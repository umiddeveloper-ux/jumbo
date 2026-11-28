namespace Jumbo.Shared.Entities;

// A game of a topic that a learner left before the end: where he was, so that he can go on at the next visit (on any device). One row per learner,
// game and topic; it is deleted when the game is won or lost, when the learner starts over, and it is ignored (and removed) after
// GameProgressRules.ExpiryDays. The State is a JSON text that only the game that wrote it understands (the server checks that it is an object with a
// version `v` and that it is small); Done / Total are what the badges show ("4 of 15").
public class GameProgress
{
    public int GameProgressId { get; set; }

    public int UserId { get; set; }

    // findme | listen | phrase | sequence | steps (Jumbo.Shared.GameProgressRules.Games)
    public string Game { get; set; } = null!;

    public int SubjectId { get; set; }

    public string State { get; set; } = "{}";

    public int Done { get; set; }

    public int Total { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
