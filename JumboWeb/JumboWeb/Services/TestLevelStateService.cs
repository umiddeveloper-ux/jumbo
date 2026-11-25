using Jumbo.Shared;

namespace JumboWeb.Services;

/// <summary>
/// The answers of the last level test ("Test my level"), kept for the page of results (Scoped: one instance per connection).
/// </summary>
public class TestLevelStateService
{
    public List<PlacementAnswer> Answers { get; private set; } = new();
    public List<double> Shares { get; private set; } = new();
    public PlacementResult? Result { get; private set; }

    // shares: the share of right answers in each block that was taken (A1, then A2, B1, B2 as far as the learner got).
    public void SetResults(List<PlacementAnswer> answers, List<double> shares)
    {
        Answers = answers;
        Shares = shares;
        Result = PlacementScoring.Evaluate(shares);
    }

    // The share of right answers (0..1) in one skill over all the blocks taken; null when the skill had no questions.
    public double? Share(string skill)
    {
        var of = Answers.Where(a => a.Question.Skill == skill).ToList();
        return of.Count == 0 ? null : of.Count(a => a.IsCorrect) / (double)of.Count;
    }

    public List<PlacementAnswer> Mistakes => Answers.Where(a => !a.IsCorrect).ToList();
}

public class PlacementAnswer
{
    public PlacementQuestionDto Question { get; set; } = null!;

    // The index of the chosen option (-1: nothing chosen).
    public int Selected { get; set; } = -1;

    public bool IsCorrect => Selected == Question.Answer;
}
