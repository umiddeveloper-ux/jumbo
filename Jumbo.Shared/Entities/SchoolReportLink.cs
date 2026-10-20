namespace Jumbo.Shared.Entities;

// A secret link to a school's read-only report (numbers per team, no pupil names), meant for a director who
// does not log in. Anyone with the token can read it until a Teacher / Director revokes it.
public class SchoolReportLink
{
    public int SchoolReportLinkId { get; set; }

    public int SchoolId { get; set; }

    // 256-bit random, URL-safe.
    public string Token { get; set; } = null!;

    public int CreatedById { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? RevokedAt { get; set; }
}
