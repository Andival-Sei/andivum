using System.Globalization;

namespace Andivum_Windows.Finance;

public static class FinanceMoney
{
    public static long ParseMinorUnits(string value, string currency)
    {
        var normalized = value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).Replace(',', '.');
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
        var minor = decimal.Round(
            amount * (scale switch { 0 => 1m, 3 => 1000m, _ => 100m }),
            0,
            MidpointRounding.AwayFromZero);
        return checked((long)minor);
    }
}
