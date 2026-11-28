namespace Jumbo.Shared.Dtos;

public class AwardItem
{
    public string Title { get; set; } = string.Empty;
    public string SvgImage { get; set; } = string.Empty;
    public bool IsAchieved { get; set; }

    // The LP the award paid (shown in the pop-up of a new award).
    public int LpBonus { get; set; }

    // The picture of the award as a file of the site (for "Book N: Completed": the frame of the book with its number, e.g. "/frames/award-03.svg"); null for the other awards.
    // SvgImage stays as it was for the clients that draw it themselves.
    public string? ImageUrl { get; set; }
}
