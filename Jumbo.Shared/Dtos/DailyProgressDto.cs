namespace Jumbo.Shared.Dtos;

public class DailyProgressDto
{
    public int GoalWords { get; set; }

    public int TodayWords { get; set; }

    public bool GoalMetToday { get; set; }

    // Consecutive days with the goal met. 0 once a day has been missed.
    public int Streak { get; set; }

    // Streak freezes left (one is used by itself when a day is missed).
    public int FreezeCount { get; set; }

    // Streak is alive but today's goal is not met yet.
    public bool StreakAtRisk { get; set; }

    // Last 7 days, oldest first, ending today.
    public List<DailyDayDto> Week { get; set; } = [];
}

public class DailyDayDto
{
    public DateOnly Date { get; set; }

    public int Words { get; set; }

    public bool GoalMet { get; set; }
}
