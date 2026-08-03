using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Andivum_Windows.Finance;

public enum FinanceTransactionType
{
    Income,
    Expense,
    Transfer,
}

public sealed record FinanceDraft(
    FinanceTransactionType Type,
    string Title,
    string OccurredOn,
    string Currency,
    long TotalMinor,
    IReadOnlyList<FinanceDraftItem> Items);

public sealed record FinanceDraftItem(
    string Name,
    decimal Quantity,
    long LineTotalMinor,
    string CategorySlug)
{
    public long UnitPriceMinor => Quantity <= 0
        ? 0
        : checked((long)decimal.Round(
            LineTotalMinor / Quantity,
            0,
            MidpointRounding.AwayFromZero));
}

public sealed record FinanceCategory(
    string Id,
    string Slug,
    string Name,
    string Type,
    string? ParentId);

public sealed record FinanceTransaction(
    string Id,
    string Title,
    string Type,
    string OccurredOn,
    string Currency,
    long TotalMinor,
    string Source,
    IReadOnlyList<FinanceDraftItem> Items)
{
    public string DisplayTotal => (TotalMinor / (Currency is "JPY" or "KRW" ? 1m : 100m))
        .ToString("0.##") + " " + Currency;
}

public sealed class FinanceDraftItemEditor : INotifyPropertyChanged
{
    private string name;
    private string lineTotalText;
    private string categorySlug;
    private IReadOnlyList<FinanceCategory> categoryChoices;

    public FinanceDraftItemEditor(
        string name,
        string lineTotalText,
        string categorySlug,
        IReadOnlyList<FinanceCategory>? categoryChoices = null)
    {
        this.name = name;
        this.lineTotalText = lineTotalText;
        this.categorySlug = categorySlug;
        this.categoryChoices = categoryChoices ?? [];
    }

    public string Name
    {
        get => name;
        set => Set(ref name, value);
    }

    public string LineTotalText
    {
        get => lineTotalText;
        set => Set(ref lineTotalText, value);
    }

    public string CategorySlug
    {
        get => categorySlug;
        set => Set(ref categorySlug, value);
    }

    public IReadOnlyList<FinanceCategory> CategoryChoices
    {
        get => categoryChoices;
        set => Set(ref categoryChoices, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
