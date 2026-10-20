namespace Jumbo.Shared;

public static class PhoneNumbers
{
    public const int MaxDigits = 15;     // E.164: the longest international number

    /// <summary>
    /// Keeps a leading "+", the digits (at most 15) and the spaces between them; everything else is dropped.
    /// Returns null when nothing is left. The website's input mask already does this; the server repeats it for any client.
    /// </summary>
    public static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var sb = new System.Text.StringBuilder();
        var digits = 0;
        foreach (var ch in value.Trim())
        {
            if (ch == '+' && sb.Length == 0) sb.Append(ch);
            else if (char.IsAsciiDigit(ch) && digits < MaxDigits) { sb.Append(ch); digits++; }
            else if (ch == ' ' && sb.Length > 0 && sb[^1] != ' ') sb.Append(ch);
        }
        var result = sb.ToString().Trim();
        return digits == 0 ? null : result;
    }
}
