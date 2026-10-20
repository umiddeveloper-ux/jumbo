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

    /// <summary>True when both texts are the same translation (empty texts never match).</summary>
    public static bool SameMeaning(string? a, string? b)
    {
        var x = Normalize(a);
        return x.Length > 0 && x == Normalize(b);
    }
}
