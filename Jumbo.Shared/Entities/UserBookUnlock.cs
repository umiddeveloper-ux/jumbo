namespace Jumbo.Shared.Entities;

// A book the learner opened by passing the 3-step check over the previous book. Book 1 is always open, and so is any book the
// learner has already played in (or a lower one): those need no row.
public class UserBookUnlock
{
    public int UserBookUnlockId { get; set; }

    public int UserId { get; set; }

    public int BookNumber { get; set; }

    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
}
