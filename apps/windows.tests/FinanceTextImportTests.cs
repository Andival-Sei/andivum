using Andivum_Windows.Finance;
using Xunit;

namespace Andivum_Windows.Tests;

public sealed class FinanceTextImportTests
{
    [Fact]
    public void ParsesSemicolonCsvWithDecimalComma()
    {
        var categories = new[]
        {
            new FinanceCategory("1", "food.groceries", "Продукты", "expense", null),
        };

        var result = FinanceTextImport.TryCreateDraft(
            "Хлеб;food.groceries;123,45",
            "receipt.csv",
            categories,
            out var draft);

        Assert.True(result);
        Assert.Equal(12_345, draft.TotalMinor);
        Assert.Equal("food.groceries", draft.Items[0].CategorySlug);
    }

    [Fact]
    public void ParsesPlainTextReceiptLine()
    {
        var result = FinanceTextImport.TryCreateDraft(
            "Кофе 250.50 RUB",
            "receipt.txt",
            Array.Empty<FinanceCategory>(),
            out var draft);

        Assert.True(result);
        Assert.Equal(25_050, draft.TotalMinor);
        Assert.Equal("Кофе", draft.Items[0].Name);
    }
}
