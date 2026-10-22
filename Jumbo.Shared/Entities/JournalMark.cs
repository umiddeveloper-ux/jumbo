namespace Jumbo.Shared.Entities;

/// <summary>
/// A mark in the teacher's journal of topics ("Журнал сдачи тем"): the pupil has passed this topic (15 words) orally to a teacher on this date.
/// It belongs to the pupil, not to a team, so it stays when the pupil changes team. One mark per pupil and topic; the teacher who set it is kept.
/// </summary>
public class JournalMark
{
    public int UserId { get; set; }

    public int SubjectId { get; set; }

    /// <summary>The date the topic was passed (the day of the app, UTC+5).</summary>
    public DateOnly PassedOn { get; set; }

    public int TeacherId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
