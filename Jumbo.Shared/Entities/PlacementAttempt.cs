namespace Jumbo.Shared.Entities;

// One attempt of the level test (CEFR) by a signed-in learner. Visitors without an account are not stored. The stage is worked out on the server
// from the shares (Jumbo.Shared.PlacementScoring), never taken from the client.
public class PlacementAttempt
{
    public int PlacementAttemptId { get; set; }

    public int UserId { get; set; }

    public DateTime TakenAt { get; set; }

    // starter | a1 | a1plus | a2 | a2plus | b1 | b1plus | b2 | b2plus
    public string Stage { get; set; } = null!;

    // A1 | A2 | B1 | B2
    public string Level { get; set; } = null!;

    public int RecommendedBook { get; set; }

    // The share of right answers (0..1) in each block; null for a block that was not taken (the next block follows only after a good one).
    public double? ShareA1 { get; set; }
    public double? ShareA2 { get; set; }
    public double? ShareB1 { get; set; }
    public double? ShareB2 { get; set; }
}
