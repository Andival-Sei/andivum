using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Andivum_Windows.Finance;

public sealed record GeminiRequest(string ApiKeyHeader, string Body);

public static class GeminiReceiptParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public static GeminiRequest BuildRequest(
        byte[] document,
        string mimeType,
        IReadOnlyCollection<FinanceCategory> categories,
        string apiKey,
        string model = "gemini-2.5-flash")
    {
        if (document.Length == 0)
        {
            throw new ArgumentException("Document is empty.", nameof(document));
        }
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("Gemini API key is required.", nameof(apiKey));
        }
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            throw new ArgumentException("Document MIME type is required.", nameof(mimeType));
        }

        var allowedCategories = categories
            .Where(category => !string.IsNullOrWhiteSpace(category.Slug))
            .Select(category => new
            {
                category.Slug,
                category.Name,
                category.Type,
            })
            .ToArray();
        var schema = new
        {
            type = "OBJECT",
            properties = new
            {
                title = new { type = "STRING" },
                type = new { type = "STRING", @enum = new[] { "income", "expense" } },
                occurred_on = new { type = "STRING" },
                currency = new { type = "STRING" },
                total_minor = new { type = "INTEGER" },
                items = new
                {
                    type = "ARRAY",
                    items = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            name = new { type = "STRING" },
                            quantity = new { type = "NUMBER" },
                            unit_price_minor = new { type = "INTEGER" },
                            line_total_minor = new { type = "INTEGER" },
                            category_slug = new { type = "STRING" },
                        },
                        required = new[] { "name", "quantity", "unit_price_minor", "line_total_minor", "category_slug" },
                    },
                },
            },
            required = new[] { "title", "type", "occurred_on", "currency", "total_minor", "items" },
        };
        var prompt = """
            Extract one personal-finance receipt or invoice into the JSON schema.
            Return only JSON. Never invent an amount or date. If a value is not visible,
            use an empty string and the UI will ask the user. total_minor and every money
            field are integer minor units in the currency. type must be income or expense.
            category_slug must be exactly one of the allowed categories below; otherwise
            use other.expense or income.other. The result is a draft, not an authorization.
            Allowed categories:
            """ + JsonSerializer.Serialize(allowedCategories, JsonOptions);
        var body = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = prompt },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = mimeType,
                                data = Convert.ToBase64String(document),
                            },
                        },
                    },
                },
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = schema,
                temperature = 0.1,
            },
        };
        return new GeminiRequest(
            apiKey,
            JsonSerializer.Serialize(body, JsonOptions));
    }

    public static async Task<FinanceDraft> ParseAsync(
        HttpClient httpClient,
        byte[] document,
        string mimeType,
        IReadOnlyCollection<FinanceCategory> categories,
        string apiKey,
        string model = "gemini-2.5-flash",
        CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(document, mimeType, categories, apiKey, model);
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent")
        {
            Content = new StringContent(request.Body, Encoding.UTF8, "application/json"),
        };
        message.Headers.Add("x-goog-api-key", request.ApiKeyHeader);
        using var response = await httpClient.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                "Gemini could not analyze the document. Check the provider settings or try again later.");
        }

        return ParseResponse(
            await response.Content.ReadAsStringAsync(cancellationToken),
            categories);
    }

    public static FinanceDraft ParseResponse(
        string response,
        IReadOnlyCollection<FinanceCategory> categories)
    {
        using var document = JsonDocument.Parse(response);
        var candidateText = document.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();
        if (string.IsNullOrWhiteSpace(candidateText))
        {
            throw new InvalidOperationException("Gemini returned an empty finance draft.");
        }

        var json = candidateText.Trim();
        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            json = json.Trim('`', ' ', 'j', 's', 'o', 'n', '\r', '\n');
        }
        var draft = JsonSerializer.Deserialize<GeminiDraft>(json, JsonOptions)
            ?? throw new InvalidOperationException("Gemini returned an invalid finance draft.");
        var expectedType = (draft.Type ?? string.Empty).ToLowerInvariant() switch
        {
            "income" => "income",
            "expense" => "expense",
            _ => throw new InvalidOperationException("Gemini returned an unsupported transaction type."),
        };
        var available = categories
            .Where(category => category.Type.Equals(expectedType, StringComparison.OrdinalIgnoreCase))
            .Select(category => category.Slug)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var items = draft.Items?.Select(item =>
        {
            if (string.IsNullOrWhiteSpace(item.Name) ||
                item.Quantity <= 0 ||
                item.LineTotalMinor < 0 ||
                !available.Contains(item.CategorySlug ?? string.Empty))
            {
                throw new InvalidOperationException("Gemini returned an invalid finance item.");
            }
            return new FinanceDraftItem(
                item.Name,
                item.Quantity,
                item.LineTotalMinor,
                item.CategorySlug!);
        }).ToArray() ?? throw new InvalidOperationException("Gemini returned no finance items.");

        return new FinanceDraft(
            expectedType.Equals("income", StringComparison.Ordinal)
                ? FinanceTransactionType.Income
                : FinanceTransactionType.Expense,
            string.IsNullOrWhiteSpace(draft.Title) ? "Без названия" : draft.Title.Trim(),
            draft.OccurredOn ?? string.Empty,
            (draft.Currency ?? string.Empty).ToUpperInvariant(),
            draft.TotalMinor,
            items);
    }

    private sealed record GeminiDraft(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("occurred_on")] string? OccurredOn,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("total_minor")] long TotalMinor,
        [property: JsonPropertyName("items")] IReadOnlyList<GeminiItem>? Items);

    private sealed record GeminiItem(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("quantity")] decimal Quantity,
        [property: JsonPropertyName("line_total_minor")] long LineTotalMinor,
        [property: JsonPropertyName("category_slug")] string? CategorySlug);
}
