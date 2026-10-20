namespace Jumbo.Shared.Entities;

// One row per user per day: how many distinct words were practised and whether the daily goal
// (as it was at the time) was reached. Kept for the week calendar.
public class UserDailyProgress
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public DateOnly Date { get; set; }

    public int WordsCount { get; set; }

    public bool GoalMet { get; set; }
}
