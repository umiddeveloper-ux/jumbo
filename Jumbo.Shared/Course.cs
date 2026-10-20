namespace Jumbo.Shared;

/// <summary>What the current edition of the course contains.</summary>
public static class Course
{
    /// <summary>
    /// Number of books on sale in this edition (books 1-10). The pages list exactly these; later books (11 and up) come with
    /// later editions of the course, together with their words in SeedData.
    /// </summary>
    public const int BookCount = 10;
}
