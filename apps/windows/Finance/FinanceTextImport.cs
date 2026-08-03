using System.Globalization;

namespace Andivum_Windows.Finance;

public static class FinanceTextImport
{
    public static bool TryCreateDraft(
        string text,
        string fileName,
        IReadOnlyCollection<FinanceCategory> categories,
        out FinanceDraft draft)
    {
        draft = null!;
        var lines = text
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var title = lines.FirstOrDefault(line => line.StartsWith("Subject:", StringComparison.OrdinalIgnoreCase))?
            .Split(':', 2)[1].Trim();
        title ??= Path.GetFileNameWithoutExtension(fileName);
        var items = new List<FinanceDraftItem>();
        foreach (var line in lines)
        {
            var fields = line.Contains(';')
                ? line.Split(';', StringSplitOptions.TrimEntries)
                : line.Contains('\t')
                    ? line.Split('\t', StringSplitOptions.TrimEntries)
                    : [line];
            var amountField = fields[^1].Trim();
            decimal amount;
            if (fields.Length == 1)
            {
                var amountMatch = System.Text.RegularExpressions.Regex.Match(
                    line,
                    @"(?<!\d)(\d+(?:[.,]\d{1,2})?)(?!\d)\s*(?:₽|RUB|руб)?$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!amountMatch.Success)
                {
                    continue;
                }

                amountField = amountMatch.Groups[1].Value;
                fields = [line[..amountMatch.Index].Trim(' ', '-', ':')];
                if (!decimal.TryParse(
                        amountField.Replace(',', '.'),
                        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture,
                        out amount))
                {
                    continue;
                }
            }
            else if (!decimal.TryParse(
                         amountField.Replace(',', '.'),
                         NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                         CultureInfo.InvariantCulture,
                         out amount))
            {
                continue;
            }

            if (amount < 0)
            {
                continue;
            }
            var minor = checked((long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
            if (minor <= 0)
            {
                continue;
            }
            var category = fields.Length >= 3 && categories.Any(item =>
                item.Slug.Equals(fields[1], StringComparison.OrdinalIgnoreCase))
                ? fields[1]
                : "other.expense";
            items.Add(new FinanceDraftItem(fields[0], 1m, minor, category));
        }

        if (items.Count == 0)
        {
            return false;
        }

        draft = new FinanceDraft(
            FinanceTransactionType.Expense,
            string.IsNullOrWhiteSpace(title) ? "Импорт" : title,
            DateTimeOffset.Now.ToString("yyyy-MM-dd"),
            "RUB",
            items.Sum(item => item.LineTotalMinor),
            items);
        return true;
    }
}
