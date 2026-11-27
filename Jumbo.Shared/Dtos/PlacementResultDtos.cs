namespace Jumbo.Shared.Dtos;

// The learner (signed in) sends the share of right answers of each block he took (block 1 = A1, then A2, B1, B2). The server works out the stage.
public class SavePlacementResultRequest
{
    public List<double> Shares { get; set; } = [];
}

// A stored attempt, as the learner sees it.
public class PlacementSavedDto
{
    public string Stage { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public int RecommendedBook { get; set; }
    public DateTime TakenAt { get; set; }

    // Per block A1, A2, B1, B2: null for a block that was not taken.
    public double?[] Shares { get; set; } = new double?[4];
}

// GET api/placement/my: the latest attempt (null when none) and how many there are.
public class MyPlacementDto
{
    public PlacementSavedDto? Latest { get; set; }
    public int Count { get; set; }
}

// What a teacher sees next to a pupil: the stage of the latest attempt and when it was.
public class PlacementBadgeDto
{
    public string Stage { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public DateTime TakenAt { get; set; }
    public double?[] Shares { get; set; } = new double?[4];
}
