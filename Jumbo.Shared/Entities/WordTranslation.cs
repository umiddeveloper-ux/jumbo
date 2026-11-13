namespace Jumbo.Shared.Entities;

/// <summary>
/// The translation of a word of the Turkish edition that has no row of its own in the other languages: a Turkish word is joined to
/// its translations by the picture (<see cref="Word.SvgImageId"/>), and the words of the Turkish books that are not in the English
/// books (or whose sentence is not the translation of the English one) have a picture of their own. For those pictures the word
/// and the example sentence in English, Russian and Uzbek are kept here (the API answers from this table when the Words table has no
/// row of that picture in that language).
/// </summary>
public class WordTranslation
{
    public int WordTranslationId { get; set; }

    public int SvgImageId { get; set; }

    public int LanguageId { get; set; }

    public string WordName { get; set; } = null!;

    public string? Example { get; set; }
}
