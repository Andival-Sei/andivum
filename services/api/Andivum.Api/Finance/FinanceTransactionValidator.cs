namespace Andivum.Api.Finance;

public static class FinanceTransactionValidator
{
    public static FinanceValidationResult Validate(
        FinanceTransactionDraft draft,
        IReadOnlyCollection<FinanceCategorySnapshot> categories)
    {
        if (string.IsNullOrWhiteSpace(draft.Title))
        {
            return FinanceValidationResult.Invalid("Transaction title is required.");
        }

        if (!IsIsoCurrency(draft.Currency))
        {
            return FinanceValidationResult.Invalid("Currency must be an ISO 4217 code.");
        }

        if (draft.TotalMinor <= 0 || draft.Items.Count == 0)
        {
            return FinanceValidationResult.Invalid(
                "Transaction must have a positive total and at least one item.");
        }

        if (draft.Items.Any(item =>
                string.IsNullOrWhiteSpace(item.Name) ||
                item.Quantity <= 0 ||
                item.UnitPriceMinor < 0 ||
                item.LineTotalMinor < 0))
        {
            return FinanceValidationResult.Invalid("Transaction items contain invalid values.");
        }

        try
        {
            if (draft.Items.Sum(item => item.LineTotalMinor) != draft.TotalMinor)
            {
                return FinanceValidationResult.Invalid(
                    "Item totals must equal the transaction total.");
            }
        }
        catch (OverflowException)
        {
            return FinanceValidationResult.Invalid("Transaction amount is too large.");
        }

        var expectedType = draft.Type switch
        {
            FinanceTransactionType.Income => FinanceCategoryType.Income,
            FinanceTransactionType.Expense => FinanceCategoryType.Expense,
            FinanceTransactionType.Transfer => FinanceCategoryType.Transfer,
            _ => throw new ArgumentOutOfRangeException(),
        };

        var categoriesBySlug = categories
            .Where(category => !string.IsNullOrWhiteSpace(category.Slug))
            .ToDictionary(category => category.Slug, StringComparer.OrdinalIgnoreCase);

        if (draft.Items.Any(item =>
                !categoriesBySlug.TryGetValue(item.CategorySlug, out var category) ||
                category.Type != expectedType))
        {
            return FinanceValidationResult.Invalid(
                "Category type does not match the transaction type.");
        }

        return FinanceValidationResult.Valid();
    }

    private static bool IsIsoCurrency(string value) =>
        value.Length == 3 && value.All(character => character is >= 'A' and <= 'Z');
}
