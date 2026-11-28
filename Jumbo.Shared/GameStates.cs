using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jumbo.Shared;

// What each game writes into the saved state of an unfinished game (GameProgress.State, a JSON text the server only keeps). The words are kept as
// their ids in the order of the round, so a resumed game shows the same words; `lang` is the language that is studied (the ids belong to it: when it
// is another one, or a word is not in the topic any more, the saved game is dropped and a new one starts). The words are put together again from the
// topic when the game is resumed, so the translation and the picture are always the current ones. What costs a life or LP is NOT kept here: the
// server counts lives and LP when the mistake happens, and a resumed game never charges it again.
public static class GameStates
{
    public const int Version = 1;

    private static readonly JsonSerializerOptions Options = new() { DefaultIgnoreCondition = JsonIgnoreCondition.Never };

    public static string Write<T>(T state) => JsonSerializer.Serialize(state, Options);

    /// <summary>The state, or null when the text is not one this version understands.</summary>
    public static T? Read<T>(string? json) where T : class, IGameState
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            // the version must be written in the text itself (a missing one would silently become the default of the class)
            using (var doc = JsonDocument.Parse(json))
                if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("v", out var v)
                    || !v.TryGetInt32(out var version) || version != Version) return null;
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException) { return null; }
    }
}

public interface IGameState
{
    int V { get; }
}

// Find me: six cards are on the screen, a found word is replaced by the next one until all the words have come.
public class FindMeState : IGameState
{
    [JsonPropertyName("v")] public int V { get; set; } = GameStates.Version;
    [JsonPropertyName("lang")] public int Lang { get; set; }
    // the words on the screen, in the order of the cards
    [JsonPropertyName("shown")] public List<int> Shown { get; set; } = [];
    // the words that have not come yet, in the order they will come
    [JsonPropertyName("unseen")] public List<int> Unseen { get; set; } = [];
    // the words on the screen that are already ticks (only at the end, when nothing is left to bring)
    [JsonPropertyName("matched")] public List<int> Matched { get; set; } = [];
    // the word that is asked now
    [JsonPropertyName("target")] public int Target { get; set; }
    [JsonPropertyName("found")] public int Found { get; set; }
    // all the words of the game, also the ones that are found already (they are put in the learner's words when the game is won)
    [JsonPropertyName("words")] public List<int> Words { get; set; } = [];
}

// Listen and find: 15 steps, each one asks one word; the words of the topic come round again when there are fewer than 15.
public class ListenState : IGameState
{
    [JsonPropertyName("v")] public int V { get; set; } = GameStates.Version;
    [JsonPropertyName("lang")] public int Lang { get; set; }
    // the words of the game (the cards of a step are taken from them)
    [JsonPropertyName("words")] public List<int> Words { get; set; } = [];
    // the word of each step, in order
    [JsonPropertyName("steps")] public List<int> Steps { get; set; } = [];
    // steps done
    [JsonPropertyName("step")] public int Step { get; set; }
}

// Build the phrase: one round for each word that has an example sentence.
public class PhraseState : IGameState
{
    [JsonPropertyName("v")] public int V { get; set; } = GameStates.Version;
    [JsonPropertyName("lang")] public int Lang { get; set; }
    // the word of each round, in order
    [JsonPropertyName("rounds")] public List<int> Rounds { get; set; } = [];
    // rounds done
    [JsonPropertyName("step")] public int Step { get; set; }
}

// Remember the order: up to four rounds, each one a few words shown one after another.
public class SequenceState : IGameState
{
    [JsonPropertyName("v")] public int V { get; set; } = GameStates.Version;
    [JsonPropertyName("lang")] public int Lang { get; set; }
    // the words of each round in the order they are shown (the right order of the answer)
    [JsonPropertyName("rounds")] public List<List<int>> Rounds { get; set; } = [];
    // rounds done
    [JsonPropertyName("round")] public int Round { get; set; }
}

// Steps (the ladder): one word after another, a mistake ends the game; two helps, each once.
public class StepsState : IGameState
{
    [JsonPropertyName("v")] public int V { get; set; } = GameStates.Version;
    [JsonPropertyName("lang")] public int Lang { get; set; }
    // the words in the order of the steps
    [JsonPropertyName("words")] public List<int> Words { get; set; } = [];
    // steps done
    [JsonPropertyName("q")] public int Question { get; set; }
    // the helps that can still be used
    [JsonPropertyName("ff")] public bool FiftyFifty { get; set; } = true;
    [JsonPropertyName("aud")] public bool Audience { get; set; } = true;
}
