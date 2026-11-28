namespace Jumbo.Shared;

/// <summary>
/// The two percents of the learner's progress, as the home page shows them. Both are whole numbers that never lie at the ends:
/// 0 only when nothing is done, 100 only when everything is done, and at least 1 as soon as something is done.
/// </summary>
public static class LearningPercent
{
    /// <summary>The words of the course: learned / total, rounded to the nearest whole percent (as the home page's label does).</summary>
    public static int Course(int learned, int total)
    {
        if (learned <= 0 || total <= 0) return 0;
        if (learned >= total) return 100;
        return Math.Clamp((int)Math.Round(learned * 100.0 / total, MidpointRounding.AwayFromZero), 1, 99);
    }

    /// <summary>A book: the parts done / all the parts of its topics (each topic has <c>UserSubjectProgress.Parts</c> parts), rounded down (as the book card does).</summary>
    public static int Book(int partsDone, int partsTotal)
    {
        if (partsDone <= 0 || partsTotal <= 0) return 0;
        if (partsDone >= partsTotal) return 100;
        return Math.Clamp((int)(partsDone * 100.0 / partsTotal), 1, 99);
    }
}
