using System.Globalization;

namespace Jumbo.Utilities;

// Compact display of LP points once they get big (same rule as the website): 999 -> "999", 1000 -> "1K",
// 10500 -> "10,5K", 2_300_000 -> "2,3M". The value is cut (not rounded) to one decimal.
public static class PointsFormat
{
    public static string Compact(int value)
    {
        if (value < 1000) return value.ToString(CultureInfo.InvariantCulture);
        if (value < 1_000_000) return Scaled(value, 1000, "K");
        return Scaled(value, 1_000_000, "M");
    }

    private static string Scaled(int value, int unit, string suffix)
    {
        var tenths = (long)value * 10 / unit;
        var whole = tenths / 10;
        var frac = tenths % 10;
        return frac == 0 ? $"{whole}{suffix}" : $"{whole},{frac}{suffix}";
    }
}
