using System.Globalization;

namespace Andivum.Api.Finance;

public static class FinanceMoney
{
    public static long ParseMinorUnits(string value, string currency)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Amount is required.", nameof(value));
        }

        var normalized = value
            .Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(',', '.');
        if (!decimal.TryParse(
                normalized,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            throw new FormatException("Amount is not a valid number.");
        }

        var scale = currency.ToUpperInvariant() switch
        {
            "BHD" or "IQD" or "JOD" or "KWD" or "LYD" or "OMR" or "TND" => 3,
            "CLP" or "DJF" or "GNF" or "ISK" or "JPY" or "KMF" or "KRW" or "PYG" or "RWF" or "UGX" or "VND" or "VUV" or "XAF" or "XOF" or "XPF" => 0,
            _ => 2,
        };

        var multiplier = scale switch
        {
            0 => 1m,
            2 => 100m,
            3 => 1000m,
            _ => throw new InvalidOperationException("Unsupported currency scale."),
        };
        var minor = decimal.Round(amount * multiplier, 0, MidpointRounding.AwayFromZero);
        if (minor > long.MaxValue || minor < long.MinValue)
        {
            throw new OverflowException("Amount is too large.");
        }

        return (long)minor;
    }
}
