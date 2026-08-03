using System.Text.Json;
using System.Text.Json.Serialization;

namespace Andivum_Windows.Finance;

public static class FinanceJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    public static string CreateTransactionPayload(
        FinanceDraft draft,
        string accountId,
        string source = "manual",
        string? importFingerprint = null)
    {
        if (string.IsNullOrWhiteSpace(accountId))
        {
            throw new ArgumentException("Account is required.", nameof(accountId));
        }
        if (string.IsNullOrWhiteSpace(draft.Title))
        {
            throw new ArgumentException("Transaction title is required.");
        }

        if (draft.TotalMinor <= 0 || draft.Items.Count == 0)
        {
            throw new ArgumentException(
                "Transaction must have a positive total and at least one item.",
                nameof(draft));
        }

        if (draft.Items.Sum(item => item.LineTotalMinor) != draft.TotalMinor)
        {
            throw new ArgumentException("Items must equal the transaction total.");
        }

        var payload = new FinanceCreateTransactionPayload(
            accountId,
            draft.Type.ToString().ToLowerInvariant(),
            draft.Title.Trim(),
            draft.OccurredOn,
            draft.Currency.ToUpperInvariant(),
            draft.TotalMinor,
            source,
            importFingerprint,
            draft.Items.Select(item => new FinanceCreateItemPayload(
                item.Name.Trim(),
                item.Quantity,
                item.UnitPriceMinor,
                item.LineTotalMinor,
                item.CategorySlug)).ToArray());

        return JsonSerializer.Serialize(payload, Options);
    }

    private sealed record FinanceCreateTransactionPayload(
        [property: JsonPropertyName("account_id")] string AccountId,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("occurred_on")] string OccurredOn,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("total_minor")] long TotalMinor,
        [property: JsonPropertyName("source")] string Source,
        [property: JsonPropertyName("import_fingerprint")] string? ImportFingerprint,
        [property: JsonPropertyName("items")] IReadOnlyList<FinanceCreateItemPayload> Items);

    private sealed record FinanceCreateItemPayload(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("quantity")] decimal Quantity,
        [property: JsonPropertyName("unit_price_minor")] long UnitPriceMinor,
        [property: JsonPropertyName("line_total_minor")] long LineTotalMinor,
        [property: JsonPropertyName("category_slug")] string CategorySlug);
}
