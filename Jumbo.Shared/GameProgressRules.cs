using System.Text.Json;

namespace Jumbo.Shared;

// The rules of the saved state of an unfinished game (one place for the API, the website and the app).
public static class GameProgressRules
{
    // The games of a topic, one key each (the same as in the address of the game: /little-polyglot/games/{key}/{topic}).
    public const string FindMe = "findme";
    public const string Listen = "listen";
    public const string Phrase = "phrase";
    public const string Sequence = "sequence";
    public const string Steps = "steps";

    public static readonly IReadOnlyList<string> Games = [FindMe, Listen, Phrase, Sequence, Steps];

    // A saved game is kept for two weeks; after that it is as if it had never been saved.
    public const int ExpiryDays = 14;

    public const int MaxStateBytes = 32 * 1024;
    public const int MaxTotal = 200;

    public static DateTime ExpiresBefore(DateTime nowUtc) => nowUtc.AddDays(-ExpiryDays);

    public static bool IsGame(string? game) => game is not null && Games.Contains(game);

    // The name the server's "game won" call uses (MarkGameWonRequest.GameType) -> the key of the saved game.
    public static string? KeyOfGameType(string? gameType) => gameType switch
    {
        "FindMe" => FindMe,
        "ListenAndFind" => Listen,
        "Phrase" => Phrase,
        "Sequence" => Sequence,
        "Steps" => Steps,
        _ => null
    };

    /// <summary>Is this a state the server may keep: a JSON object not bigger than 32 KB with a whole number `v` (the version of its format).</summary>
    public static bool IsValidState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state) || System.Text.Encoding.UTF8.GetByteCount(state) > MaxStateBytes) return false;
        try
        {
            using var doc = JsonDocument.Parse(state);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty("v", out var v)
                   && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var version) && version is >= 1 and <= 100;
        }
        catch (JsonException) { return false; }
    }

    public static bool IsValidProgress(int done, int total) => total is >= 1 and <= MaxTotal && done >= 0 && done <= total;
}
