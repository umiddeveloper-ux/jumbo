namespace Jumbo.Shared.Dtos;

/// <summary>An ad for the admin page (the list; the picture is only in the one that is opened for editing).</summary>
public class AdDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Html { get; set; } = string.Empty;
    public bool HasImage { get; set; }
    public string? ImageDataUrl { get; set; }
    public string Mode { get; set; } = "fixed";
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public int IntervalMinutes { get; set; }
    public int DailyAtMinutes { get; set; }
    public string? Lang { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    // statistics (the list)
    public int Viewers { get; set; }
    public int Showings { get; set; }
    public DateTime? LastShownAt { get; set; }
}

public class SaveAdRequest
{
    public string Title { get; set; } = string.Empty;

    /// <summary>The text as html; the server cleans it (Jumbo.Shared.SafeHtml).</summary>
    public string Html { get; set; } = string.Empty;

    /// <summary>A new picture as a data: url (png, jpeg, webp, gif; up to 1 MB). Null keeps the picture the ad has.</summary>
    public string? ImageDataUrl { get; set; }

    public bool RemoveImage { get; set; }
    public string Mode { get; set; } = "fixed";
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public int IntervalMinutes { get; set; }

    /// <summary>Daily: minutes from midnight (Tashkent time) from which it is shown.</summary>
    public int DailyAtMinutes { get; set; }
    public string? Lang { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>The ad that is to be shown to the learner now.</summary>
public class AdForUserDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Html { get; set; } = string.Empty;

    /// <summary>The picture as a data: url, or null.</summary>
    public string? ImageDataUrl { get; set; }
}
