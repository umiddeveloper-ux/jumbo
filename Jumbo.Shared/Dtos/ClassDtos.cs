namespace Jumbo.Shared.Dtos;

public class CreateClassRequest
{
    public string Name { get; set; } = null!;
}

public class SetArchivedRequest
{
    public bool IsArchived { get; set; }
}

// A learner picks a team from the school's list (the school comes with the team).
public class SelectTeamRequest
{
    public int ClassId { get; set; }
}

// One entry of the team picker.
public class TeamOptionDto
{
    public int ClassId { get; set; }
    public string Name { get; set; } = null!;
}

// The learner's own team and whether the teacher has confirmed it yet.
public class MyTeamDto
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = null!;
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = null!;
    public string Region { get; set; } = null!;
    public string? TeacherName { get; set; }
    public bool Approved { get; set; }
}

// Teacher's team card.
public class ClassDto
{
    public int ClassId { get; set; }
    public string Name { get; set; } = null!;
    public bool IsArchived { get; set; }
    public string? TeacherName { get; set; }

    // Approved learners only.
    public int StudentCount { get; set; }

    // Learners who picked the team and wait for the teacher's decision.
    public int PendingCount { get; set; }

    // Approved learners who practised at least one word in the last 7 days.
    public int ActiveWeek { get; set; }

    // Average of the approved learners' exam scores in the last 30 days; null when nobody took an exam.
    public int? AvgExamPercent { get; set; }
}

public class PendingStudentDto
{
    public int UserId { get; set; }
    public string Name { get; set; } = null!;
    public string? Username { get; set; }
}

public class ClassStudentDto
{
    public int UserId { get; set; }
    public string Name { get; set; } = null!;
    public string? Username { get; set; }
    public int LpPoints { get; set; }
    public int Streak { get; set; }

    // Distinct words practised over the last 7 days (app-time days) and on how many of those days.
    public int WordsWeek { get; set; }
    public int ActiveDaysWeek { get; set; }
    public DateOnly? LastActiveDate { get; set; }

    public int ExamCount { get; set; }
    public int? LastExamPercent { get; set; }
    public int? AvgExamPercent { get; set; }
    public DateTime? LastExamAt { get; set; }

    // The book the pupil is working in now: the highest one with any played game or practised word; null before the first lesson.
    public int? CurrentBook { get; set; }
}

public class ClassDetailDto
{
    public ClassDto Class { get; set; } = null!;
    public List<ClassStudentDto> Students { get; set; } = new();
    public List<PendingStudentDto> Pending { get; set; } = new();
}

// ── One pupil's progress, book by book (the teacher opens it from the team table) ──────────────

public class StudentTopicDto
{
    public int SubjectId { get; set; }
    public string Name { get; set; } = null!;
    public int SequenceNumber { get; set; }
    public bool FindMe { get; set; }
    public bool Sequence { get; set; }
    public bool Listen { get; set; }
    public bool Steps { get; set; }
    public bool Phrase { get; set; }

    // All four main games won (the Phrase game is an extra one).
    public bool Done { get; set; }
    public int WordsTotal { get; set; }
    public int WordsLearned { get; set; }
}

public class StudentBookDto
{
    public int BookNumber { get; set; }
    public bool IsCurrent { get; set; }
    public int TopicsTotal { get; set; }
    public int TopicsDone { get; set; }

    // Topics where some game was won or some word practised, but not finished.
    public int TopicsStarted { get; set; }
    public int WordsTotal { get; set; }
    public int WordsLearned { get; set; }
    public int ExamCount { get; set; }
    public int? ExamBestPercent { get; set; }
    public int? ExamLastPercent { get; set; }
    public DateTime? ExamLastAt { get; set; }
    public List<StudentTopicDto> Topics { get; set; } = new();
}

public class StudentProgressDto
{
    public int UserId { get; set; }
    public string Name { get; set; } = null!;
    public string? Username { get; set; }
    public string ClassName { get; set; } = null!;
    public int LpPoints { get; set; }
    public int Streak { get; set; }
    public DateOnly? LastActiveDate { get; set; }
    public int? CurrentBook { get; set; }
    public List<StudentBookDto> Books { get; set; } = new();
}

// The learner's latest exam in one book (shown under the book on the exam setup page).
public class MyExamBookResultDto
{
    public int BookNumber { get; set; }
    public int TotalCount { get; set; }
    public int CorrectCount { get; set; }
    public int WrongCount => TotalCount - CorrectCount;
    public int Percent => TotalCount > 0 ? (int)Math.Round(CorrectCount * 100.0 / TotalCount) : 0;
    public DateTime TakenAt { get; set; }
}

public class SubmitExamResultRequest
{
    public int BookNumber { get; set; }

    // Set when the exam was taken with a teacher's code; null for a free exam.
    public int? TeacherExamId { get; set; }
    public int TotalCount { get; set; }
    public int CorrectCount { get; set; }

    // The words answered wrongly (for the team report); optional.
    public List<ExamMistakeDto> Mistakes { get; set; } = new();
}

// ── Exams started by a teacher (6-digit code) ────────────────────────────────

public class CreateTeacherExamRequest
{
    public int WordCount { get; set; }
}

public class TeacherExamSummaryDto
{
    public int TeacherExamId { get; set; }
    public string Code { get; set; } = null!;
    public int WordCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int ParticipantCount { get; set; }
    public int? AvgPercent { get; set; }
}

public class TeacherExamResultDto
{
    public int UserId { get; set; }
    public string Name { get; set; } = null!;
    public string? ClassName { get; set; }
    public int BookNumber { get; set; }
    public int TotalCount { get; set; }
    public int CorrectCount { get; set; }
    public int WrongCount => TotalCount - CorrectCount;
    public int Percent => TotalCount > 0 ? (int)Math.Round(CorrectCount * 100.0 / TotalCount) : 0;
    public DateTime TakenAt { get; set; }

    // Taken with a teacher's code (true) or as a free exam the pupil started themselves (false).
    public bool ByCode { get; set; }
}

public class TeacherExamDetailDto
{
    public TeacherExamSummaryDto Exam { get; set; } = null!;
    public List<TeacherExamResultDto> Results { get; set; } = new();
}

public class JoinTeacherExamRequest
{
    public string Code { get; set; } = string.Empty;
}

public class JoinTeacherExamResponse
{
    public int TeacherExamId { get; set; }
    public int WordCount { get; set; }
}

// The exam report of the teacher: the teams, and in a team every pupil with the words learned in each book.
// Only exams by a teacher's code count (a free exam the pupil starts themselves does not).
public static class ExamReport
{
    // Words in one book: a pupil who passed a book's exam with the best result of 50 % has learned 150 of them.
    public const int WordsPerBook = 300;
}

public class ExamTeamDto
{
    public int ClassId { get; set; }
    public string Name { get; set; } = null!;
    public bool IsArchived { get; set; }
    public int PupilCount { get; set; }

    // Pupils who have at least one exam by a teacher's code.
    public int ExaminedCount { get; set; }

    // The average, over the team's examined pupils, of each pupil's best result (%).
    public int? AvgBestPercent { get; set; }
}

public class ExamPupilRowDto
{
    public int UserId { get; set; }
    public string Name { get; set; } = null!;

    // Index 0 = Book 1 ... : the best result (%) and the words learned (WordsPerBook x best result); null = no exam of that book.
    public List<int?> Percents { get; set; } = new();
    public List<int?> Words { get; set; } = new();
}

public class ExamTeamReportDto
{
    public int ClassId { get; set; }
    public string Name { get; set; } = null!;
    public int WordsPerBook { get; set; } = ExamReport.WordsPerBook;
    public List<ExamPupilRowDto> Pupils { get; set; } = new();
}
