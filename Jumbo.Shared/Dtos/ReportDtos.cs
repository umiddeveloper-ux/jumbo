namespace Jumbo.Shared.Dtos;

// "week" = the last 7 app-days, "month" = the last 30; each is compared with the period just before it.
public class ReportPeriodDto
{
    public string Key { get; set; } = "week";
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public DateOnly PrevFrom { get; set; }
    public DateOnly PrevTo { get; set; }
}

public class ReportTotalsDto
{
    public int Students { get; set; }

    // Learners who practised at least one word in the period.
    public int Active { get; set; }
    public int Words { get; set; }
    public int ExamCount { get; set; }
    public int? AvgExamPercent { get; set; }

    // The same figures for the previous period of the same length.
    public int ActivePrev { get; set; }
    public int WordsPrev { get; set; }
    public int ExamCountPrev { get; set; }
    public int? AvgExamPercentPrev { get; set; }
}

public class ReportStudentDto
{
    public int UserId { get; set; }
    public string Name { get; set; } = null!;
    public int Words { get; set; }
    public int ActiveDays { get; set; }
    public DateOnly? LastActiveDate { get; set; }
    public int Streak { get; set; }
    public int ExamCount { get; set; }
    public int? AvgExamPercent { get; set; }

    // The book the pupil is working in now (see ClassStudentDto.CurrentBook).
    public int? CurrentBook { get; set; }

    // "active" | "slow" | "absent"
    public string Status { get; set; } = "absent";
}

public class MissedWordDto
{
    public string Word { get; set; } = null!;
    public string? Translation { get; set; }
    public int Count { get; set; }
}

// Team report: names of pupils, so it is only served to the team's teacher / the school's director.
public class TeamReportDto
{
    public string TeamName { get; set; } = null!;
    public string SchoolName { get; set; } = null!;
    public string Region { get; set; } = null!;
    public string? TeacherName { get; set; }
    public ReportPeriodDto Period { get; set; } = null!;
    public ReportTotalsDto Totals { get; set; } = null!;
    public List<ReportStudentDto> Students { get; set; } = new();
    public List<MissedWordDto> MissedWords { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class ReportTeamRowDto
{
    // Filled only in the teacher's own report (to open the report of the team); 0 in the school report that is passed on by link.
    public int ClassId { get; set; }
    public string TeamName { get; set; } = null!;
    public string? TeacherName { get; set; }
    public ReportTotalsDto Totals { get; set; } = null!;
}

// School report: figures per team only, no pupil names, safe to pass on by link.
public class SchoolReportDto
{
    public string SchoolName { get; set; } = null!;
    public string Region { get; set; } = null!;
    public ReportPeriodDto Period { get; set; } = null!;
    public ReportTotalsDto Totals { get; set; } = null!;
    public List<ReportTeamRowDto> Teams { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class ReportLinkDto
{
    public string Token { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public class ExamMistakeDto
{
    public string Word { get; set; } = null!;
    public string? Translation { get; set; }
}
