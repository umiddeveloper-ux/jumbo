namespace Jumbo.Shared.Entities;

public class UserAwards
{
    public int UserAwardsId { get; set; }

    public int UserId { get; set; }

    public int AwardsId { get; set; }

    // When the learner earned it, and when they were told about it (the pop-up of a new award; null = not told yet).
    public DateTime AwardedAt { get; set; } = DateTime.UtcNow;

    public DateTime? NotifiedAt { get; set; }
}
