namespace Jumbo.Shared;

/// <summary>
/// The JO a WON game gives, by the game: ONE table for the server (which gives them), the website and the app (which show the number on the
/// game tiles and at the end of a game). A game is named as the server's "game won" call names it (MarkGameWonRequest.GameType).
/// </summary>
public static class GameRewards
{
    public const string FindMe = "FindMe";
    public const string Sequence = "Sequence";
    public const string ListenAndFind = "ListenAndFind";
    public const string Steps = "Steps";
    public const string Phrase = "Phrase";

    public static readonly IReadOnlyDictionary<string, int> ByGameType = new Dictionary<string, int>
    {
        [FindMe] = 5, [Sequence] = 7, [ListenAndFind] = 7, [Steps] = 10, [Phrase] = 8
    };

    /// <summary>The JO of a won game by the server's game name (0 for an unknown game).</summary>
    public static int Of(string? gameType) => gameType is not null && ByGameType.TryGetValue(gameType, out var jo) ? jo : 0;

    /// <summary>The JO of a won game by the key of the game (the one in its address and in the saved state: findme, listen, ...); 0 for an unknown key.</summary>
    public static int OfKey(string? key) => key?.ToLowerInvariant() switch
    {
        GameProgressRules.FindMe => Of(FindMe),
        GameProgressRules.Listen => Of(ListenAndFind),
        GameProgressRules.Phrase => Of(Phrase),
        GameProgressRules.Sequence => Of(Sequence),
        GameProgressRules.Steps => Of(Steps),
        _ => 0
    };
}
