using System.Globalization;

namespace JumboWeb.Services;

// Compact display of LP points once they get big: 999 -> "999", 1000 -> "1K", 10500 -> "10,5K", 2_300_000 -> "2,3M".
// The decimal separator is always a comma. The value is cut (not rounded) to one decimal, so 10 999 shows as
// "10,9K" and never claims more points than the player has.
public static class PointsFormat
{
    public static string Compact(int value)
    {
        if (value < 1000) return value.ToString(CultureInfo.InvariantCulture);
        if (value < 1_000_000) return Scaled(value, 1000, "K");
        return Scaled(value, 1_000_000, "M");
    }

    // Full number with thin grouping, e.g. "10 500" (for tooltips).
    public static string Exact(int value) => value.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', ' ');

    private static string Scaled(int value, int unit, string suffix)
    {
        var tenths = (long)value * 10 / unit;          // floor to one decimal
        var whole = tenths / 10;
        var frac = tenths % 10;
        return frac == 0 ? $"{whole}{suffix}" : $"{whole},{frac}{suffix}";
    }
}
