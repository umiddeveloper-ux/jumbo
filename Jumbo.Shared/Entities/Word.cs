namespace Jumbo.Shared.Entities;

public class Word
{
    public int WordId { get; set; }

    public int SubjectId { get; set; }

    public int LanguageId { get; set; }

    public int SvgImageId { get; set; }

    public string WordName { get; set; } = null!;

    public string FirstSymbol { get; set; } = null!;

    // Position within the whole word list, starting at 1 for book 1, 301 for book 2, etc. —
    // matches the "id" field baked into the SeedData book JSON files. 0 means not yet assigned.
    public int WordNumber { get; set; }

    // CEFR-style level (e.g. "A1", "B2") from the seed JSON.
    public string? Level { get; set; }

    // An example sentence for the word, in the language of this row (the English row holds the English sentence, the Russian
    // row its Russian translation, the Uzbek row the Uzbek one). Null for words without an example.
    public string? Example { get; set; }
}
