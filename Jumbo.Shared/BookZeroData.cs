namespace Jumbo.Shared;

/// <summary>
/// "Book 0": the alphabet and the numbers up to 20, always open, with the sound. It is a reference, not a book of the course: no
/// topics, no progress, no check, and it does not count for the level, the "current book" or the unlocking of the other books.
/// </summary>
public static class BookZeroData
{
    /// <summary>A letter and how its name is said (a lone letter is often read as a word by the voice, so the name is spelled out).</summary>
    public static readonly (string Letter, string Spoken)[] Letters =
    [
        ("A", "ay"), ("B", "bee"), ("C", "see"), ("D", "dee"), ("E", "ee"), ("F", "eff"), ("G", "gee"), ("H", "aitch"),
        ("I", "eye"), ("J", "jay"), ("K", "kay"), ("L", "el"), ("M", "em"), ("N", "en"), ("O", "oh"), ("P", "pee"),
        ("Q", "cue"), ("R", "ar"), ("S", "ess"), ("T", "tee"), ("U", "you"), ("V", "vee"), ("W", "double you"),
        ("X", "ex"), ("Y", "why"), ("Z", "zee")
    ];

    /// <summary>The vowel letters are shown in colour.</summary>
    public static bool IsVowel(string letter) => letter.Length == 1 && "AEIOU".Contains(letter[0]);

    public const int MaxNumber = 20;

    private static readonly string[] En =
    [
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen",
        "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen", "twenty"
    ];

    private static readonly string[] Ru =
    [
        "ноль", "один", "два", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять", "десять", "одиннадцать", "двенадцать",
        "тринадцать", "четырнадцать", "пятнадцать", "шестнадцать", "семнадцать", "восемнадцать", "девятнадцать", "двадцать"
    ];

    private static readonly string[] Uz =
    [
        "nol", "bir", "ikki", "uch", "to'rt", "besh", "olti", "yetti", "sakkiz", "to'qqiz", "o'n", "o'n bir", "o'n ikki", "o'n uch",
        "o'n to'rt", "o'n besh", "o'n olti", "o'n yetti", "o'n sakkiz", "o'n to'qqiz", "yigirma"
    ];

    /// <summary>The English word of a number (0..20).</summary>
    public static string NumberWord(int n) => En[n];

    /// <summary>The number in the learner's language (language ids of the site: 2 Russian, 3 Uzbek); null for English.</summary>
    public static string? NumberTranslation(int n, int languageId) => languageId switch { 2 => Ru[n], 3 => Uz[n], _ => null };
}
