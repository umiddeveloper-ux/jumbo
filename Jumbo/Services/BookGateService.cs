namespace Jumbo.Services;

/// <summary>
/// The check that opens a book (same rule as on the website): three games - Find, Listen, Sequence - played over the words of ALL
/// topics of the previous book. The games are the ordinary topic games; started with a "gate" book number they take the words from
/// book gate - 1, report the win here instead of marking a topic, and return to the check page. The steps passed live as long as the
/// app does.
/// </summary>
public sealed class BookGateService
{
    public const int Steps = 3;

    private readonly Dictionary<int, HashSet<int>> _passed = new();

    /// <summary>The book the learner opened by passing the check: the home page selects it when it shows next.</summary>
    public int PendingOpenBook { get; set; }

    public int PassedCount(int target) => _passed.TryGetValue(target, out var set) ? set.Count : 0;

    public bool IsPassed(int target, int step) => _passed.TryGetValue(target, out var set) && set.Contains(step);

    public void Pass(int target, int step)
    {
        if (!_passed.TryGetValue(target, out var set)) _passed[target] = set = new HashSet<int>();
        set.Add(step);
    }

    public void Reset(int target) => _passed.Remove(target);

    /// <summary>The route of the game for a step (1 Find, 2 Listen, 3 Sequence); topic id 0: the words come from the book.</summary>
    public static string StepRoute(int step, int target) => step switch
    {
        1 => $"FindMeGamePage?gate={target}",
        2 => $"ListenGamePage?gate={target}",
        _ => $"SequenceGamePage?gate={target}"
    };
}
