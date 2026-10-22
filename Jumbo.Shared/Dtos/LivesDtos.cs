namespace Jumbo.Shared.Dtos;

/// <summary>
/// The learner's lives and LP as the server counts them. The server is the only one that changes them (a mistake, a won game, the lives
/// coming back after the block, the market); the apps and the site only show what they were answered.
/// </summary>
public class LivesStateDto
{
    public int Lives { get; set; }
    public int LpPoints { get; set; }

    /// <summary>When the lives come back (the block of 5 hours); null when there is no block.</summary>
    public DateTime? LifeRegenAt { get; set; }

    public int ShieldCount { get; set; }

    /// <summary>Silver and Gold: a mistake costs nothing.</summary>
    public bool Unlimited { get; set; }

    /// <summary>After a mistake: can the learner go on (lives left, or LP when the lives are gone; with livesOnly only the lives count).</summary>
    public bool CanPlay { get; set; }

    /// <summary>After a won game: the LP the server gave for it (0 when the game gives none or the limit of the day is reached).</summary>
    public int LpAwarded { get; set; }
}

public class MistakeRequest
{
    /// <summary>The check that opens a book: only the lives count there (the LP do not stand in for them).</summary>
    public bool LivesOnly { get; set; }
}
