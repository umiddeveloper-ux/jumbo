namespace Jumbo.Services;

/// <summary>
/// Input mask of the phone field (same rules as the website's <c>lp.phoneMask</c>): only a leading "+" and digits, at most 15 digits
/// (the international limit), grouped with spaces like "+998 90 123 45 67". Pasted text is cleaned the same way.
/// </summary>
public static class PhoneMask
{
    private static readonly string[] Codes = ["998", "996", "992", "993", "994", "380", "375", "971", "90", "82", "81", "86", "91", "49", "44", "7", "1"];

    private static readonly Dictionary<string, int[]> Groups = new()
    {
        ["998"] = [2, 3, 2, 2], ["7"] = [3, 3, 2, 2], ["996"] = [3, 2, 2, 2], ["992"] = [2, 3, 2, 2], ["993"] = [2, 2, 2, 2],
        ["994"] = [2, 3, 2, 2], ["380"] = [2, 3, 2, 2], ["375"] = [2, 3, 2, 2], ["971"] = [2, 3, 4], ["82"] = [2, 4, 4], ["81"] = [2, 4, 4],
        ["86"] = [3, 4, 4], ["91"] = [5, 5], ["90"] = [3, 3, 2, 2], ["49"] = [3, 8], ["44"] = [4, 6], ["1"] = [3, 3, 4]
    };

    public static string Format(string? raw)
    {
        raw ??= string.Empty;
        var digits = new string(raw.Where(char.IsAsciiDigit).Take(15).ToArray());
        if (digits.Length == 0) return raw.StartsWith('+') ? "+" : string.Empty;

        var cc = Codes.FirstOrDefault(c => digits.StartsWith(c, StringComparison.Ordinal)) ?? string.Empty;
        var rest = digits[cc.Length..];
        var sizes = Groups.TryGetValue(cc, out var g) ? g : [3, 3, 3, 3, 3];
        var pos = 0;
        string output;

        if (cc.Length == 0)
        {
            // a country code we do not know: its first three digits
            output = "+" + rest[..Math.Min(3, rest.Length)];
            rest = rest[Math.Min(3, rest.Length)..];
        }
        else output = "+" + cc;

        foreach (var size in sizes)
        {
            if (pos >= rest.Length) break;
            output += " " + rest.Substring(pos, Math.Min(size, rest.Length - pos));
            pos += size;
        }
        if (pos < rest.Length) output += " " + rest[pos..];
        return output;
    }
}
