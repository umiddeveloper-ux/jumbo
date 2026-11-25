namespace Jumbo.Shared;

// The level test ("Test my level"): two blocks of questions, A1 and then (only if the A1 block went well) A2.

public static class PlacementSkill
{
    public const string Vocabulary = "vocabulary";
    public const string Grammar = "grammar";
    public const string Reading = "reading";
    public const string Listening = "listening";

    public static readonly string[] All = [Vocabulary, Grammar, Reading, Listening];
}

public class PlacementQuestionDto
{
    public string Id { get; set; } = string.Empty;
    public string Skill { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;

    // Grammar / reading / listening: the question text. Vocabulary: empty (the page says "What is this?").
    public string Prompt { get; set; } = string.Empty;

    // Reading: the text (the same for the questions of one text).
    public string? PassageTitle { get; set; }
    public string? Passage { get; set; }

    // Listening: what is said (only the speech of the browser says it; the text is shown on the page only when speech is missing, and in the review).
    public List<string> Audio { get; set; } = [];

    // Vocabulary: the picture of the word.
    public int? SvgImageId { get; set; }

    public List<string> Options { get; set; } = [];
    public int Answer { get; set; }
}

public class PlacementBlockDto
{
    public string Level { get; set; } = string.Empty;
    public List<PlacementQuestionDto> Questions { get; set; } = [];
}

// What the learner is told at the end. Stage: starter | a1 | a1plus | a2 | a2plus (the texts of the page are by stage).
public record PlacementResult(string Stage, string Level, int RecommendedBook);

public static class PlacementScoring
{
    // The share of right answers in a block that counts as "this level is known" (go on to the next block) and as "some of it is known".
    public const double Known = 0.70;
    public const double Some = 0.40;

    // a1 / a2: the share of right answers (0..1) in the A1 and A2 blocks; a2 is null when the A2 block was not taken.
    // The books: 1-3 are A1, 4-6 are A2 (7-10 are B1, which this test does not reach yet).
    public static PlacementResult Evaluate(double a1, double? a2)
    {
        if (a1 < Some) return new("starter", "A1", 1);
        if (a1 < Known || a2 is null) return new("a1", "A1", 1);
        if (a2 < Some) return new("a1plus", "A1", 3);
        if (a2 < Known) return new("a2", "A2", 4);
        return new("a2plus", "A2", 6);
    }

    public static bool GoesOn(double share) => share >= Known;
}
