namespace JumboWeb.Services;

/// <summary>
/// Хранит результаты последнего пройденного теста "Test my level" (Scoped — один экземпляр на соединение),
/// чтобы передать их со страницы теста на страницу результатов без сложных query-строк.
/// </summary>
public class TestLevelStateService
{
    public List<TestLevelAnswer> Answers { get; private set; } = new();

    public void SetResults(List<TestLevelAnswer> answers) => Answers = answers;

    public int TotalCount => Answers.Count;
    public int CorrectCount => Answers.Count(a => a.IsCorrect);
    public int IncorrectCount => Answers.Count(a => !a.IsCorrect);
    public int Percentage => Answers.Count > 0 ? (int)Math.Round(CorrectCount * 100.0 / Answers.Count) : 0;
    public List<TestLevelAnswer> IncorrectAnswers => Answers.Where(a => !a.IsCorrect).ToList();
}

public class TestLevelAnswer
{
    public string StudyWord { get; set; } = string.Empty;
    public string CorrectTranslation { get; set; } = string.Empty;
    public string SelectedTranslation { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
}
