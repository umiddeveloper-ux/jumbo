namespace Jumbo.Shared;

/// <summary>
/// Two different words can mean the same in the learner's language ("bye" and "goodbye" are both "до свидания"). The games and tests
/// therefore never decide "right or wrong" by the word itself: a choice that has the same translation as the right one is right too,
/// and a wrong option is never taken from the words that share the translation of the right one.
/// </summary>
public static class WordMatch
{
    /// <summary>The translation without the differences that are not about the meaning: case, extra spaces.</summary>
    public static string Normalize(string? text)
        => string.Join(' ', (text ?? string.Empty).Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// A word typed in an exam is right when it is the word, whatever the case. For Turkish (language 4) the letters that a keyboard without
    /// Turkish letters cannot type are not required: ç ğ ı İ ö ş ü are taken as c g i i o s u, so "sut" is right for "süt".
    /// </summary>
    public static bool TypedEquals(string? entered, string? word, int studyLanguageId)
    {
        var a = (entered ?? string.Empty).Trim();
        var b = (word ?? string.Empty).Trim();
        if (studyLanguageId != 4) return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        return string.Equals(FoldTurkish(a), FoldTurkish(b), StringComparison.Ordinal);
    }

    private static string FoldTurkish(string s)
    {
        var chars = s.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            chars[i] = chars[i] switch
            {
                'ç' or 'Ç' => 'c', 'ğ' or 'Ğ' => 'g', 'ı' or 'İ' or 'I' or 'i' => 'i', 'ö' or 'Ö' => 'o',
                'ş' or 'Ş' => 's', 'ü' or 'Ü' => 'u', var c => char.ToLowerInvariant(c)
            };
        return new string(chars);
    }

    /// <summary>True when both texts are the same translation (empty texts never match).</summary>
    public static bool SameMeaning(string? a, string? b)
    {
        var x = Normalize(a);
        return x.Length > 0 && x == Normalize(b);
    }
}
