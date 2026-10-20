namespace Jumbo.Shared.Entities;

// A word the learner got wrong in an exam. Only the word pair is kept (no typed answer): it feeds the
// "most missed words" block of the team report.
public class ExamMistake
{
    public int ExamMistakeId { get; set; }

    public int ExamResultId { get; set; }

    // The word the learner was asked to write (study language) and its prompt (translation language).
    public string Word { get; set; } = null!;

    public string? Translation { get; set; }
}
