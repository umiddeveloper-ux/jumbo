namespace Jumbo.Shared.Dtos;

// Leaderboard response. Only privacy-safe fields are exposed: "Name" is "First L." (or null when the player
// hid their name), the team is shown, nothing else about the person (no phone, school, region, photo).
public class RatingDto
{
    // team | school | region | all
    public string Scope { get; set; } = "all";

    // week | all
    public string Period { get; set; } = "week";

    // The caller has no team / school / region set, so this scope cannot be computed for them.
    public bool Missing { get; set; }

    // Number of players with at least one point in this scope.
    public int Total { get; set; }

    public List<RatingEntryDto> Entries { get; set; } = [];

    // The caller's own place (null when they have no points in this scope).
    public RatingEntryDto? Me { get; set; }
}

public class RatingEntryDto
{
    public int Rank { get; set; }

    // "Aliya K." — null when the player chose to hide their name.
    public string? Name { get; set; }

    public string? Team { get; set; }

    // Inline <svg> of a preset avatar only (uploaded photos are never shared); null when the name is hidden.
    public string? AvatarSvg { get; set; }

    // Worn from the market: the frame round the avatar and the picture of the badge by the name (null when the name is hidden).
    public string? FrameKey { get; set; }
    public string? BadgeSvg { get; set; }

    public int Words { get; set; }

    public bool IsMe { get; set; }
}
