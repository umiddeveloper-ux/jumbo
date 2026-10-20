namespace Jumbo.Shared.Entities;

// An exam started by a teacher: pupils enter its 6-digit code (valid for a few minutes) and take it in a book of their
// choice. The finished attempts are ExamResult rows pointing here; free exams have no TeacherExamId.
public class TeacherExam
{
    public int TeacherExamId { get; set; }

    public int TeacherId { get; set; }

    // 6 digits. Unique only among the exams whose code is still valid.
    public string Code { get; set; } = null!;

    // Number of questions, set by the teacher.
    public int WordCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }
}
