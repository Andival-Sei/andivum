using Andivum_Windows.Finance;
using Xunit;

namespace Andivum_Windows.Tests;

public sealed class FinanceClientTests
{
    [Fact]
    public void Builds_an_atomic_rpc_payload_with_minor_units_and_items()
    {
        var draft = new FinanceDraft(
            FinanceTransactionType.Expense,
            "Покупки",
            "2026-08-03",
            "RUB",
            1_234,
            [new FinanceDraftItem("Молоко", 1m, 1_234, "food.dairy")]);

        var payload = FinanceJson.CreateTransactionPayload(draft, "account-1");

        Assert.Contains("\"total_minor\":1234", payload);
        Assert.Contains("\"category_slug\":\"food.dairy\"", payload);
        Assert.Contains("\"account_id\":\"account-1\"", payload);
    }

    [Fact]
    public void Rejects_a_draft_before_network_when_items_do_not_add_up()
    {
        var draft = new FinanceDraft(
            FinanceTransactionType.Expense,
            "Покупки",
            "2026-08-03",
            "RUB",
            1_234,
            [new FinanceDraftItem("Молоко", 1m, 120, "food.dairy")]);

        var exception = Assert.Throws<ArgumentException>(
            () => FinanceJson.CreateTransactionPayload(draft, "account-1"));

        Assert.Equal("Items must equal the transaction total.", exception.Message);
    }

    [Fact]
    public void Rejects_a_draft_without_a_title()
    {
        var draft = new FinanceDraft(
            FinanceTransactionType.Expense,
            " ",
            "2026-08-03",
            "RUB",
            100,
            [new FinanceDraftItem("Молоко", 1m, 100, "food.dairy")]);

        var exception = Assert.Throws<ArgumentException>(
            () => FinanceJson.CreateTransactionPayload(draft, "account-1"));

        Assert.Equal("Transaction title is required.", exception.Message);
    }
}
