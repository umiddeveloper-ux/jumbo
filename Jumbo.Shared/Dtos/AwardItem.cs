namespace Jumbo.Shared.Dtos;

public class AwardItem
{
    public string Title { get; set; } = string.Empty;
    public string SvgImage { get; set; } = string.Empty;
    public bool IsAchieved { get; set; }

    // The LP the award paid (shown in the pop-up of a new award).
    public int LpBonus { get; set; }
}
