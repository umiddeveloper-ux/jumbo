namespace Jumbo.Shared.Entities;

// A Telegram chat (usually a school director's) that receives the school's weekly report from the Jumbo bot.
// A teacher / director creates the row in the cabinet (it only has a Code then) and passes the deep link
// t.me/<bot>?start=<Code> on; the first chat that opens it becomes the ChatId. The director never logs in.
public class TelegramConnection
{
    public int TelegramConnectionId { get; set; }

    // The school of a staff / recipient link. Null for a pupil's own link (a pupil may have no school at all).
    public int? SchoolId { get; set; }

    // Random URL-safe one-time code of the deep link; useless once a chat is attached.
    public string Code { get; set; } = null!;

    // Null until somebody opens the link.
    public long? ChatId { get; set; }

    // Telegram first name of the person who connected (shown to the teacher, never to anybody else).
    public string? TelegramName { get; set; }

    // Language of the bot's messages to this chat: ru | uz | en.
    public string Lang { get; set; } = "ru";

    // Set for a personal link (a teacher / director connecting their own account: they get pupil requests and
    // alerts and can act from the chat). Null for a school recipient who only receives the weekly report.
    public int? UserId { get; set; }

    // When the last "who stopped practising" alert went out (at most one every 3 days).
    public DateTime? LastAlertAt { get; set; }

    // The time of the newest exam already reported to this chat (a teacher gets each exam once).
    public DateTime? LastExamAt { get; set; }

    // When the last "teams without activity" warning went out (a director gets it at most once a week).
    public DateTime? LastTeamAlertAt { get; set; }

    // A pupil can switch the evening reminder off with /remind.
    public bool NotifyOff { get; set; }

    // When the last evening reminder went out (one per day).
    public DateTime? LastReminderAt { get; set; }

    public int CreatedById { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ConnectedAt { get; set; }

    // When the last weekly digest went out (stops a second send in the same week).
    public DateTime? LastDigestAt { get; set; }
}
