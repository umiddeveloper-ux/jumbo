namespace Jumbo.Shared.Dtos;

// Saving a game that is left half way (PUT api/games/progress).
public class SaveGameProgressRequest
{
    // findme | listen | phrase | sequence | steps
    public string Game { get; set; } = string.Empty;

    public int SubjectId { get; set; }

    // A JSON object with a version `v`; only the game that wrote it reads it.
    public string State { get; set; } = string.Empty;

    // How far the learner is (shown as "4 of 15"): 0..Total, Total 1..200.
    public int Done { get; set; }
    public int Total { get; set; }
}

// One saved game with its state (GET api/games/progress?game=&subjectId=).
public class GameProgressDto
{
    public string Game { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public string State { get; set; } = string.Empty;
    public int Done { get; set; }
    public int Total { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// The saved games of the learner without their state, for the badges (GET api/games/progress/unfinished).
public class GameProgressSummaryDto
{
    public string Game { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public int Done { get; set; }
    public int Total { get; set; }
    public DateTime UpdatedAt { get; set; }
}
