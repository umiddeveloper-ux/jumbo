namespace Jumbo.Shared.Entities;

/// <summary>
/// An advertisement that the admin writes and the site and the apps show in a window to learners of the free version (Bronze).
/// It is shown by time: <see cref="AdModes.Fixed"/> - once, from a moment on; <see cref="AdModes.Periodic"/> - again and again, every
/// <see cref="IntervalMinutes"/> minutes (counted per learner from the last time he saw it).
/// </summary>
public class Ad
{
    public int AdId { get; set; }

    public string Title { get; set; } = null!;

    /// <summary>The text: html of a safe subset (see Jumbo.Shared.SafeHtml; it is cleaned when saved).</summary>
    public string Html { get; set; } = string.Empty;

    /// <summary>An optional picture (png, jpeg, webp or gif, up to 1 MB), kept in the database.</summary>
    public byte[]? ImageData { get; set; }

    public string? ImageType { get; set; }

    /// <summary>"fixed" or "periodic".</summary>
    public string Mode { get; set; } = AdModes.Fixed;

    /// <summary>The moment it starts to be shown (UTC). For a fixed ad this is "the time".</summary>
    public DateTime StartsAt { get; set; } = DateTime.UtcNow;

    /// <summary>It is not shown after this moment (UTC); null = until the admin stops it.</summary>
    public DateTime? EndsAt { get; set; }

    /// <summary>Periodic: minutes between two showings to the same learner.</summary>
    public int IntervalMinutes { get; set; }

    /// <summary>Daily: the time of the day (minutes from midnight, Tashkent time UTC+5) from which the ad is shown, once a day to every learner.</summary>
    public int DailyAtMinutes { get; set; }

    /// <summary>Only for learners whose language of the app is this one (ru / en / uz); null = for everybody.</summary>
    public string? Lang { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class AdModes
{
    public const string Fixed = "fixed";
    public const string Periodic = "periodic";
    public const string Daily = "daily";
}

/// <summary>When a learner saw an ad last and how many times: what "once" and "every N minutes" are counted from.</summary>
public class AdView
{
    public int UserId { get; set; }

    public int AdId { get; set; }

    public DateTime LastShownAt { get; set; }

    public int Count { get; set; }
}
