namespace Jumbo.Shared.Entities;

// A word the user practised on a given (app-time) day. Used only to count DISTINCT words per day,
// so replaying the same game does not inflate the daily goal. Rows older than today are purged.
public class UserDailyWord
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public DateOnly Date { get; set; }

    public int WordId { get; set; }
}
