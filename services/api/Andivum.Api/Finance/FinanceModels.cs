namespace Andivum.Api.Finance;

public enum FinanceTransactionType
{
    Income,
    Expense,
    Transfer,
}

public enum FinanceCategoryType
{
    Income,
    Expense,
    Transfer,
}

public sealed record FinanceTransactionItemDraft(
    string Name,
    decimal Quantity,
    long UnitPriceMinor,
    long LineTotalMinor,
    string CategorySlug);

public sealed record FinanceTransactionDraft(
    FinanceTransactionType Type,
    string Title,
    DateOnly OccurredOn,
    string Currency,
    long TotalMinor,
    IReadOnlyList<FinanceTransactionItemDraft> Items);

public sealed record FinanceCategorySnapshot(
    string Slug,
    FinanceCategoryType Type,
    string? ParentSlug);

public sealed record FinanceValidationResult(bool IsValid, string? Error)
{
    public static FinanceValidationResult Valid() => new(true, null);

    public static FinanceValidationResult Invalid(string error) => new(false, error);
}
