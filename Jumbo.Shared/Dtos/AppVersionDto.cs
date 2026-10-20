namespace Jumbo.Shared.Dtos;

public class AppVersionDto
{
    public string Platform { get; set; } = string.Empty;
    public int LatestCode { get; set; }
    public int MinCode { get; set; }
    public string? Url { get; set; }
    public string? NotesRu { get; set; }
    public string? NotesEn { get; set; }
    public string? NotesUz { get; set; }
}
