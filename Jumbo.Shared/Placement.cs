namespace Jumbo.Shared;

// The level test ("Test my level"): blocks of questions, one block per level (A1, A2, B1, B2). The next block follows only when the
// block before it went well.

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

    // Grammar / reading / listening and the text vocabulary questions: the question. Picture vocabulary: empty (the page says "What is this?").
    public string Prompt { get; set; } = string.Empty;

    // Reading: the text (the same for the questions of one text).
    public string? PassageTitle { get; set; }
    public string? Passage { get; set; }

    // Listening: what is said (only the speech of the browser says it; the text is shown on the page only when speech is missing, and in the review).
    public List<string> Audio { get; set; } = [];

    // Listening: when a recorded voice exists, the id of the question it is kept under (the page plays /placement-audio/{AudioId}).
    public int? AudioId { get; set; }

    // Vocabulary with a picture: the picture of the word.
    public int? SvgImageId { get; set; }

    public List<string> Options { get; set; } = [];
    public int Answer { get; set; }
}

public class PlacementBlockDto
{
    public string Level { get; set; } = string.Empty;
    public List<PlacementQuestionDto> Questions { get; set; } = [];
}

// What the learner is told at the end. Stage: starter | a1 | a1plus | a2 | a2plus | b1 | b1plus | b2 | b2plus (the texts of the page are by stage).
public record PlacementResult(string Stage, string Level, int RecommendedBook);

public static class PlacementScoring
{
    public static readonly string[] Levels = ["A1", "A2", "B1", "B2"];

    // The share of right answers in a block that counts as "this level is known" (go on to the next block) and as "some of it is known".
    public const double Known = 0.70;
    public const double Some = 0.40;

    // shares[i]: the share of right answers (0..1) in the block of Levels[i]; only the blocks that were taken.
    // The books: 1-3 are A1, 4-6 are A2, 7-10 are B1 (the course has no B2 books: the last book is the one to revise from).
    public static PlacementResult Evaluate(IReadOnlyList<double> shares)
    {
        if (shares.Count == 0 || shares[0] < Some) return new("starter", "A1", 1);

        var known = 0;                                    // how many levels in a row are known
        while (known < shares.Count && shares[known] >= Known) known++;

        if (known == 0) return new("a1", "A1", 1);
        if (known == Levels.Length) return new("b2plus", "B2", 10);

        // The next level was tried: some of it is known (the learner is at that level), or not yet (strong at the level just passed).
        var some = known < shares.Count && shares[known] >= Some;
        return (known, some) switch
        {
            (1, false) => new("a1plus", "A1", 3),
            (1, true) => new("a2", "A2", 4),
            (2, false) => new("a2plus", "A2", 6),
            (2, true) => new("b1", "B1", 7),
            (3, false) => new("b1plus", "B1", 9),
            _ => new("b2", "B2", 10),
        };
    }

    public static bool GoesOn(double share) => share >= Known;
}
