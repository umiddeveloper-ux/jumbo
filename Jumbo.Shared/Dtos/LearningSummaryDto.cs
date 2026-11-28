namespace Jumbo.Shared.Dtos;

/// <summary>
/// What the "welcome back" dialog tells a learner who has started learning: the language, how much of the course is learned and where they are.
/// The percents follow the rules of <see cref="LearningPercent"/>, the same numbers the home page shows.
/// </summary>
public class LearningSummaryDto
{
    /// <summary>At least one word is learned in the study language; otherwise nothing is shown.</summary>
    public bool HasStarted { get; set; }

    public int StudyLanguageId { get; set; }

    /// <summary>"en", "ru", "uz" or "tr": the client writes the name of the language in the language of its own screen.</summary>
    public string StudyLanguageCode { get; set; } = "en";

    public int LearnedWords { get; set; }

    /// <summary>All the words of the course in the study language.</summary>
    public int TotalWords { get; set; }

    public int CoursePercent { get; set; }

    /// <summary>The book the learner is in (the first open book with a topic that is not fully won).</summary>
    public int CurrentBook { get; set; }

    /// <summary>"A1", "A2", "B1" of that book.</summary>
    public string Level { get; set; } = "A1";

    /// <summary>How much of the current book is passed (the three games of each topic).</summary>
    public int BookPercent { get; set; }

    public int BookTopics { get; set; }

    public int BookTopicsPassed { get; set; }

    /// <summary>Days in a row with the daily goal met (0 when a day was missed).</summary>
    public int Streak { get; set; }
}
