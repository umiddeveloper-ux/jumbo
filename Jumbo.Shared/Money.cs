using System.Globalization;

namespace Jumbo.Shared;

/// <summary>Amounts of money (so'm, whole numbers): written with spaces between the thousands, 1250000 = "1 250 000".</summary>
public static class Money
{
    public static string Format(long amount) => amount.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', ' ');
}
