namespace Jumbo.Shared.Dtos;

public class JournalTopicDto
{
    public int SubjectId { get; set; }

    /// <summary>The number of the topic in the book, 1..20 (the column of the journal).</summary>
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public int WordFrom { get; set; }
    public int WordTo { get; set; }
}

public class JournalPupilDto
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class JournalMarkDto
{
    public int UserId { get; set; }
    public int SubjectId { get; set; }
    public DateOnly PassedOn { get; set; }
}

/// <summary>One sheet of the journal: a team and a book. Rows are the pupils, columns the 20 topics of the book.</summary>
public class JournalDto
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string SchoolName { get; set; } = string.Empty;
    public string? TeacherName { get; set; }

    public int Book { get; set; }
    public string Level { get; set; } = string.Empty;          // A1, A2, B1 ...
    public int WordFrom { get; set; }
    public int WordTo { get; set; }
    public string SchoolYear { get; set; } = string.Empty;     // 2026–2027

    /// <summary>The books that exist (the tabs of the journal).</summary>
    public List<int> Books { get; set; } = new();
    public List<JournalTopicDto> Topics { get; set; } = new();
    public List<JournalPupilDto> Pupils { get; set; } = new();
    public List<JournalMarkDto> Marks { get; set; } = new();

    /// <summary>Topics the pupil has fully passed in the app (all three games): a hint for the teacher before the oral test.</summary>
    public List<JournalMarkDto> AppPassed { get; set; } = new();
}

/// <summary>Puts a mark (PassedOn = the date) or takes it away (PassedOn = null).</summary>
public class SetJournalMarkRequest
{
    public int ClassId { get; set; }
    public int PupilId { get; set; }
    public int SubjectId { get; set; }
    public DateOnly? PassedOn { get; set; }
}
