namespace Jumbo.Shared.Dtos;

public class WordItem
{
    // The number of the word in the printed book (same as in the web dictionary); 0 = unknown.
    public int Number { get; set; }
    public string Word { get; set; } = string.Empty;
    public string Translate { get; set; } = string.Empty;
    public string SvgHtml { get; set; } = string.Empty;

    // A sentence with the word (in the language being studied); empty when there is none.
    public string Example { get; set; } = string.Empty;
    public bool HasExample => !string.IsNullOrWhiteSpace(Example);
}
