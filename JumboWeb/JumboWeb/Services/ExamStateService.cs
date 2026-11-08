namespace JumboWeb.Services;

/// <summary>
/// Хранит результаты последнего пройденного экзамена (Scoped — один экземпляр на соединение),
/// чтобы передать их со страницы теста на страницу результатов без сложных query-строк.
/// </summary>
public class ExamStateService
{
    public List<ExamAnswer> Answers { get; private set; } = new();

    // Which exam it was, so "take it again" can start the same one right away.
    public int BookNumber { get; private set; }
    public int WordCount { get; private set; }

    // Set when the exam was taken with a teacher's code; SentToTeacher says whether the result reached the server.
    public int? TeacherExamId { get; private set; }
    public bool SentToTeacher { get; private set; }

    // type (the translation is typed) | test (one of four answers)
    public string Kind { get; private set; } = "type";

    public void SetResults(List<ExamAnswer> answers, int bookNumber = 0, int wordCount = 0, int? teacherExamId = null, bool sent = true, string kind = "type")
    {
        Kind = kind;
        Answers = answers;
        BookNumber = bookNumber;
        WordCount = wordCount;
        TeacherExamId = teacherExamId;
        SentToTeacher = sent;
    }

    public int TotalCount => Answers.Count;
    public int CorrectCount => Answers.Count(a => a.IsCorrect);
    public int IncorrectCount => Answers.Count(a => !a.IsCorrect);
    public int Percentage => Answers.Count > 0 ? (int)Math.Round(CorrectCount * 100.0 / Answers.Count) : 0;
    public List<ExamAnswer> IncorrectAnswers => Answers.Where(a => !a.IsCorrect).ToList();
}

public class ExamAnswer
{
    public string TranslationWord { get; set; } = string.Empty;
    public string CorrectWord { get; set; } = string.Empty;
    public string EnteredWord { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
}
