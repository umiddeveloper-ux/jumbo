namespace Jumbo.Shared.Entities;

public class Award
{
    public int AwardId { get; set; }

    public int LanguageId { get; set; }

    public string AwardName { get; set; } = null!;

    public int SvgImageId { get; set; }
}
