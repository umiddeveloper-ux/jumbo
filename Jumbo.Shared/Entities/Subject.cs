namespace Jumbo.Shared.Entities;

public class Subject
{
    public int SubjectId { get; set; }

    public int BookId { get; set; }

    public string SubjectName { get; set; } = null!;

    public int SvgImageId { get; set; }

    public int SequenceNumber { get; set; }

    public bool IsPassed { get; set; }

    public string VideoUrl { get; set; } = string.Empty;
}
