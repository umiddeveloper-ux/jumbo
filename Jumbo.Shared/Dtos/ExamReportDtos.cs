namespace Jumbo.Shared.Dtos;

// The exam report of a whole school by teams, for the director (behind the secret link of the school report: /r/<token>/exams).
// It holds pupil names, like the teacher's own reports. Only exams taken with a teacher's code count (a free exam is not stored),
// by approved pupils of the school's teams that are not archived.

public static class ExamReportPeriods
{
    public const string Week = "week";
    public const string Month = "month";
    public const string Term = "term";

    /// <summary>"week" (the last 7 app-days), "month" (the last 30) or "term" (since 1 September of the school year); anything else is "month".</summary>
    public static string Normalize(string? key) => key?.Trim().ToLowerInvariant() switch
    {
        Week => Week,
        Term => Term,
        _ => Month
    };

    /// <summary>The first day of the period that ends on <paramref name="today"/> (an app-day, UTC+5).</summary>
    public static DateOnly From(string key, DateOnly today) => key switch
    {
        Week => today.AddDays(-6),
        Term => today.Month >= 9 ? new DateOnly(today.Year, 9, 1) : new DateOnly(today.Year - 1, 9, 1),
        _ => today.AddDays(-29)
    };
}

public class ExamPeriodDto
{
    public string Key { get; set; } = ExamReportPeriods.Month;
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
}

public class ExamReportTotalsDto
{
    public int Teams { get; set; }

    // Approved pupils in the teams.
    public int Pupils { get; set; }

    // Pupils who took at least one exam in the period.
    public int Participants { get; set; }

    // Exams (by a teacher's code) in which at least one pupil of the school's teams took part in the period; an exam taken by pupils of
    // two teams is counted once here and once in each team.
    public int ExamsHeld { get; set; }

    // Finished attempts in the period.
    public int Attempts { get; set; }

    // The average result (%) of the attempts of the period.
    public int? AvgPercent { get; set; }
}

public class ExamReportExamDto
{
    // The day the teacher started the exam (app-day, UTC+5).
    public DateOnly Date { get; set; }
    public string? TeacherName { get; set; }

    // How many questions the teacher set.
    public int Questions { get; set; }

    // Pupils of this team who took it and the average result (%) of their attempts.
    public int Participants { get; set; }
    public int? AvgPercent { get; set; }
}

public class ExamReportPupilDto
{
    public string Name { get; set; } = null!;

    // In the period: the attempts, the best and the last result (%), and the day of the last one.
    public int Exams { get; set; }
    public int? BestPercent { get; set; }
    public int? LastPercent { get; set; }
    public DateOnly? LastDate { get; set; }

    // All the books together, all time: the words learned (WordsPerBook x the best result of every book), as in the teacher's team exam report.
    public int WordsTotal { get; set; }
}

public class ExamReportTeamDto
{
    public string TeamName { get; set; } = null!;
    public string? TeacherName { get; set; }
    public int Pupils { get; set; }
    public int Participants { get; set; }
    public int Attempts { get; set; }
    public int? AvgPercent { get; set; }

    // The exams in which pupils of this team took part in the period, oldest first.
    public List<ExamReportExamDto> Exams { get; set; } = new();

    // Pupils with an exam in the period first (the best first), then the others (a dash in the table).
    public List<ExamReportPupilDto> PupilRows { get; set; } = new();
}

public class SchoolExamReportDto
{
    public string SchoolName { get; set; } = null!;
    public string Region { get; set; } = null!;
    public ExamPeriodDto Period { get; set; } = null!;
    public ExamReportTotalsDto Totals { get; set; } = null!;

    // By team name.
    public List<ExamReportTeamDto> Teams { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}
