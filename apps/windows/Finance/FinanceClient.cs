using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Andivum_Windows.Auth;

namespace Andivum_Windows.Finance;

public sealed record FinanceCreateResult(
    bool IsDuplicate,
    string? TransactionId);

public sealed class FinanceApiException(HttpStatusCode statusCode, string message)
    : InvalidOperationException(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

public sealed class FinanceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient httpClient;
    private readonly ITokenStore tokenStore;
    private readonly string publishableKey;
    private readonly Uri restBaseUri;

    public FinanceClient(
        HttpClient httpClient,
        ITokenStore tokenStore,
        Uri supabaseUrl,
        string publishableKey)
    {
        this.httpClient = httpClient;
        this.tokenStore = tokenStore;
        this.publishableKey = publishableKey;
        restBaseUri = new Uri($"{supabaseUrl.AbsoluteUri.TrimEnd('/')}/rest/v1/");
    }

    public async Task<IReadOnlyList<FinanceCategory>> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await SendAsync<IReadOnlyList<FinanceCategoryRow>>(
            HttpMethod.Get,
            new Uri(restBaseUri, "finance_categories?select=id,slug,name_ru,name_en,category_type,parent_id&order=slug.asc"),
            content: null,
            cancellationToken);
        return rows.Select(row => new FinanceCategory(
            row.Id ?? string.Empty,
            row.Slug ?? string.Empty,
            row.NameRu ?? row.NameEn ?? row.Slug ?? string.Empty,
            row.CategoryType ?? string.Empty,
            row.ParentId)).ToArray();
    }

    public async Task<IReadOnlyList<FinanceAccount>> GetAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await SendAsync<IReadOnlyList<FinanceAccount>>(
            HttpMethod.Get,
            new Uri(restBaseUri, "finance_accounts?select=id,name,account_type,currency&archived_at=is.null&order=created_at.asc"),
            content: null,
            cancellationToken);
        return rows;
    }

    public async Task<FinanceAccount> CreateAccountAsync(
        string name,
        string currency = "RUB",
        CancellationToken cancellationToken = default)
    {
        var body = JsonContent.Create(new
        {
            name = name.Trim(),
            account_type = "cash",
            currency = currency.ToUpperInvariant(),
        });
        var rows = await SendAsync<IReadOnlyList<FinanceAccount>>(
            HttpMethod.Post,
            new Uri(restBaseUri, "finance_accounts?select=id,name,account_type,currency"),
            body,
            cancellationToken,
            preferRepresentation: true);
        return rows.FirstOrDefault() ?? throw new InvalidOperationException(
            "Supabase did not return the created finance account.");
    }

    public async Task<IReadOnlyList<FinanceTransaction>> GetTransactionsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await SendAsync<IReadOnlyList<FinanceTransactionRow>>(
            HttpMethod.Get,
            new Uri(restBaseUri, "finance_transactions?select=id,title,transaction_type,occurred_on,currency,total_minor,source,finance_transaction_items(name,quantity,unit_price_minor,line_total_minor,category_id,sort_order,finance_categories(slug))&order=occurred_on.desc,created_at.desc&limit=50"),
            content: null,
            cancellationToken);
        return rows.Select(row => new FinanceTransaction(
            row.Id ?? string.Empty,
            row.Title ?? string.Empty,
            row.TransactionType ?? string.Empty,
            row.OccurredOn ?? string.Empty,
            row.Currency ?? string.Empty,
            row.TotalMinor,
            row.Source ?? "manual",
            (row.Items ?? []).OrderBy(item => item.SortOrder).Select(item => new FinanceDraftItem(
                item.Name ?? string.Empty,
                item.Quantity,
                item.LineTotalMinor,
                item.Category?.Slug ?? item.CategoryId ?? string.Empty)).ToArray())).ToArray();
    }

    public async Task<FinanceCreateResult> CreateTransactionAsync(
        FinanceDraft draft,
        string accountId,
        string source = "manual",
        string? importFingerprint = null,
        CancellationToken cancellationToken = default)
    {
        var body = new StringContent(
            FinanceJson.CreateTransactionPayload(draft, accountId, source, importFingerprint),
            System.Text.Encoding.UTF8,
            "application/json");
        var response = await SendAsync<JsonElement>(
            HttpMethod.Post,
            new Uri(restBaseUri, "rpc/finance_create_transaction"),
            body,
            cancellationToken);
        var json = response.ValueKind == JsonValueKind.Array && response.GetArrayLength() > 0
            ? response[0]
            : response;
        return new FinanceCreateResult(
            json.TryGetProperty("duplicate", out var duplicate) && duplicate.GetBoolean(),
            json.TryGetProperty("transaction_id", out var transactionId)
                ? transactionId.GetString()
                : null);
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        Uri endpoint,
        HttpContent? content,
        CancellationToken cancellationToken,
        bool preferRepresentation = false)
    {
        var token = tokenStore.Read() ?? throw new InvalidOperationException(
            "No saved Supabase session is available.");
        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new InvalidOperationException("Supabase access token is required.");
        }

        using var request = new HttpRequestMessage(method, endpoint)
        {
            Content = content,
        };
        request.Headers.Add("apikey", publishableKey);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.AccessToken);
        if (preferRepresentation)
        {
            request.Headers.Add("Prefer", "return=representation");
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new FinanceApiException(
                response.StatusCode,
                $"Finance request failed with HTTP {(int)response.StatusCode}.");
        }

        return JsonSerializer.Deserialize<T>(responseBody, JsonOptions)
            ?? throw new InvalidOperationException("Finance API returned an empty response.");
    }

    public sealed record FinanceAccount(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("account_type")] string? AccountType,
        [property: JsonPropertyName("currency")] string? Currency);

    private sealed record FinanceCategoryRow(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("slug")] string? Slug,
        [property: JsonPropertyName("name_ru")] string? NameRu,
        [property: JsonPropertyName("name_en")] string? NameEn,
        [property: JsonPropertyName("category_type")] string? CategoryType,
        [property: JsonPropertyName("parent_id")] string? ParentId);

    private sealed record FinanceTransactionRow(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("transaction_type")] string? TransactionType,
        [property: JsonPropertyName("occurred_on")] string? OccurredOn,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("total_minor")] long TotalMinor,
        [property: JsonPropertyName("source")] string? Source,
        [property: JsonPropertyName("finance_transaction_items")] IReadOnlyList<FinanceTransactionItemRow>? Items);

    private sealed record FinanceTransactionItemRow(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("quantity")] decimal Quantity,
        [property: JsonPropertyName("unit_price_minor")] long UnitPriceMinor,
        [property: JsonPropertyName("line_total_minor")] long LineTotalMinor,
        [property: JsonPropertyName("category_id")] string? CategoryId,
        [property: JsonPropertyName("sort_order")] int SortOrder,
        [property: JsonPropertyName("finance_categories")] FinanceCategoryLink? Category);

    private sealed record FinanceCategoryLink(
        [property: JsonPropertyName("slug")] string? Slug);
}
