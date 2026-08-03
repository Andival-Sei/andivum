using Andivum.Api.Finance;
using Xunit;

namespace Andivum.Api.Tests;

public sealed class FinanceTransactionValidatorTests
{
    [Fact]
    public void Accepts_an_expense_whose_items_add_up_to_the_total()
    {
        var draft = new FinanceTransactionDraft(
            FinanceTransactionType.Expense,
            "Покупки",
            new DateOnly(2026, 8, 3),
            "RUB",
            1_234,
            [
                new FinanceTransactionItemDraft("Молоко", 1m, 120, 120, "food.dairy"),
                new FinanceTransactionItemDraft("Продукты", 1m, 1_114, 1_114, "food.groceries"),
            ]);

        var result = FinanceTransactionValidator.Validate(
            draft,
            [
                new FinanceCategorySnapshot("food", FinanceCategoryType.Expense, null),
                new FinanceCategorySnapshot("food.dairy", FinanceCategoryType.Expense, "food"),
                new FinanceCategorySnapshot("food.groceries", FinanceCategoryType.Expense, "food"),
            ]);

        Assert.True(result.IsValid, result.Error);
    }

    [Fact]
    public void Rejects_a_draft_when_item_totals_do_not_match_the_header()
    {
        var draft = new FinanceTransactionDraft(
            FinanceTransactionType.Expense,
            "Покупки",
            new DateOnly(2026, 8, 3),
            "RUB",
            1_234,
            [new FinanceTransactionItemDraft("Молоко", 1m, 120, 120, "food.dairy")]);

        var result = FinanceTransactionValidator.Validate(
            draft,
            [new FinanceCategorySnapshot("food.dairy", FinanceCategoryType.Expense, "food")]);

        Assert.False(result.IsValid);
        Assert.Equal("Item totals must equal the transaction total.", result.Error);
    }

    [Fact]
    public void Rejects_an_expense_item_with_an_income_category()
    {
        var draft = new FinanceTransactionDraft(
            FinanceTransactionType.Expense,
            "Покупки",
            new DateOnly(2026, 8, 3),
            "RUB",
            100,
            [new FinanceTransactionItemDraft("Зарплата", 1m, 100, 100, "income.salary")]);

        var result = FinanceTransactionValidator.Validate(
            draft,
            [new FinanceCategorySnapshot("income.salary", FinanceCategoryType.Income, "income")]);

        Assert.False(result.IsValid);
        Assert.Equal("Category type does not match the transaction type.", result.Error);
    }

    [Theory]
    [InlineData("12.34", 1234)]
    [InlineData("12,34", 1234)]
    [InlineData("100", 10000)]
    public void Converts_decimal_user_input_to_minor_units(string input, long expected)
    {
        Assert.Equal(expected, FinanceMoney.ParseMinorUnits(input, "RUB"));
    }
}
