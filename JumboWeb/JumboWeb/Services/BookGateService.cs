namespace JumboWeb.Services;

// The check that opens a book: three games (Find, Listen, Sequence) played over the words of ALL topics of the previous book.
// The games are the ordinary topic games; with "?gate=N" in their address they take the words from book N - 1, report the win here
// instead of marking a topic, and lead back to the check page. The steps passed live as long as the circuit does.
public class BookGateService
{
    public const int Steps = 3;

    private readonly Dictionary<int, HashSet<int>> _passed = new();

    public int PassedCount(int target) => _passed.TryGetValue(target, out var set) ? set.Count : 0;

    public bool IsPassed(int target, int step) => _passed.TryGetValue(target, out var set) && set.Contains(step);

    public void Pass(int target, int step)
    {
        if (!_passed.TryGetValue(target, out var set)) _passed[target] = set = new HashSet<int>();
        set.Add(step);
    }

    public void Reset(int target) => _passed.Remove(target);

    public static string GateUrl(int target) => $"/little-polyglot/unlock/{target}";

    // Step 1: Find, 2: Listen, 3: Sequence. The topic id in the address is 0: the words come from the book.
    public static string StepUrl(int step, int target) => step switch
    {
        1 => $"/little-polyglot/games/findme/0?gate={target}",
        2 => $"/little-polyglot/games/listen/0?gate={target}",
        _ => $"/little-polyglot/games/sequence/0?gate={target}"
    };
}
