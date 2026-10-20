namespace Jumbo.Shared.Entities;

// One finished exam. ClassId is a snapshot of the student's class at the time, so reports stay correct
// after the student leaves or switches class.
public class ExamResult
{
    public int ExamResultId { get; set; }

    public int UserId { get; set; }

    public int? ClassId { get; set; }

    public int BookNumber { get; set; }

    public int TotalCount { get; set; }

    public int CorrectCount { get; set; }

    // Set when the exam was taken with a teacher's code; null for a free exam.
    public int? TeacherExamId { get; set; }

    public DateTime TakenAt { get; set; } = DateTime.UtcNow;

    public int Percent => TotalCount > 0 ? (int)Math.Round(CorrectCount * 100.0 / TotalCount) : 0;
}
