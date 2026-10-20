namespace Jumbo.Shared.Dtos;

public class ExamWordItem
{
    public int WordId { get; set; }
    public int SvgImageId { get; set; }
    public string StudyWord { get; set; } = string.Empty;
    // Other words of the language with the same translation: typing one of them is right too.
    public List<string> AlternativeWords { get; set; } = new();

    public string TranslationWord { get; set; } = string.Empty;
}
