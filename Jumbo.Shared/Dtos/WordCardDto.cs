namespace Jumbo.Shared.Dtos;

/// <summary>A word of a topic with everything a game needs to show it: the translation, the example and the picture, in one piece.</summary>
public class WordCardDto
{
    public int WordId { get; set; }
    public int SubjectId { get; set; }
    public int SvgImageId { get; set; }
    public string WordName { get; set; } = string.Empty;
    public string? Example { get; set; }

    // The same word in the translation language (empty when the language has no such word).
    public int TranslationWordId { get; set; }
    public string? TranslationWord { get; set; }
    public string? TranslationExample { get; set; }

    public string Svg { get; set; } = string.Empty;

    // The picture as a file (relative address, e.g. "/pics/0f3a9c21b7d4e8a6.webp") when it exists; Svg is empty then only when the server setting
    // Pictures:OmitSvg is on. Old clients use Svg.
    public string? ImageUrl { get; set; }
}
