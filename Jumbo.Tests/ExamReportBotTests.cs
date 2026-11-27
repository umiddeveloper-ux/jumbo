using System.Text.Json;
using Jumbo.Api.Services.Telegram;
using Jumbo.Shared.Dtos;

namespace Jumbo.Tests;

// The bot's /examreport: the command is in the director's menu only, and the summary text and the buttons are what the director expects.
public class ExamReportBotTests
{
    private static ExamReportTeamDto Team(string name, int pupils, int participants, int attempts, int? avg) => new()
    {
        TeamName = name, Pupils = pupils, Participants = participants, Attempts = attempts, AvgPercent = avg
    };

    private static SchoolExamReportDto Report(string period, params ExamReportTeamDto[] teams)
    {
        var withExams = teams.Where(t => t.Attempts > 0).ToList();
        return new SchoolExamReportDto
        {
            SchoolName = "School <5>", Region = "Test",
            Period = new ExamPeriodDto { Key = period, From = new DateOnly(2026, 11, 1), To = new DateOnly(2026, 11, 30) },
            Totals = new ExamReportTotalsDto
            {
                Teams = teams.Length, Pupils = teams.Sum(t => t.Pupils), Participants = teams.Sum(t => t.Participants),
                Attempts = teams.Sum(t => t.Attempts), ExamsHeld = withExams.Count,
                AvgPercent = withExams.Count == 0 ? null : (int)Math.Round(withExams.Average(t => t.AvgPercent ?? 0))
            },
            Teams = teams.ToList()
        };
    }

    // ── the menu ────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("ru")]
    [InlineData("uz")]
    [InlineData("en")]
    public void The_exam_report_command_is_in_the_menu_of_a_director_only(string lang)
    {
        Assert.Contains(TelegramProfile.MenuFor("director", lang), c => c.Command == "examreport");
        foreach (var kind in new[] { "teacher", "recipient", "pupil", "nobody" })
            Assert.DoesNotContain(TelegramProfile.MenuFor(kind, lang), c => c.Command == "examreport");
    }

    [Fact]
    public void The_command_has_a_description_in_every_language_within_the_limits_of_Telegram()
    {
        foreach (var lang in TelegramTexts.Languages)
        {
            var text = TelegramProfile.MenuFor("director", lang).Single(c => c.Command == "examreport").Text;
            Assert.InRange(text.Length, 3, 256);
        }
    }

    [Fact]
    public void Every_text_of_the_exam_report_exists_in_all_three_languages()
    {
        foreach (var lang in TelegramTexts.Languages)
            foreach (var key in new[]
            {
                "examreport_p_week", "examreport_p_month", "examreport_p_term", "examreport_none", "examreport_btn", "examreport_btn_week",
                "examreport_btn_term", "digest_examlink"
            })
                Assert.False(string.IsNullOrWhiteSpace(TelegramTexts.Get(lang, key)), $"{lang}/{key}");
    }

    // ── the summary ─────────────────────────────────────────────────────────
    [Fact]
    public void A_period_without_exams_says_so_and_has_no_figures()
    {
        var text = ExamReportMessage.Build(Report("month", Team("A", 10, 0, 0, null)), "en");
        Assert.Contains("no exams", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exams held", text);
    }

    [Fact]
    public void The_summary_has_the_totals_the_participation_the_average_and_the_best_and_the_weakest_team()
    {
        var report = Report("month", Team("A", 10, 8, 14, 80), Team("B", 12, 6, 9, 55), Team("C", 8, 4, 5, 70));
        var text = ExamReportMessage.Build(report, "en");

        Assert.Contains("Exams held: 3, attempts: 28", text);
        Assert.Contains("Took part: 18 of 30 pupils", text);
        Assert.Contains("Average result: 68%", text);        // (80 + 55 + 70) / 3 = 68.3
        Assert.Contains("Best team: A — 80%", text);
        Assert.Contains("Lowest: B — 55%", text);
        Assert.Contains("for the month", text);
    }

    [Fact]
    public void With_one_team_that_took_exams_there_is_no_weakest_team()
    {
        var text = ExamReportMessage.Build(Report("month", Team("A", 10, 8, 14, 80), Team("B", 12, 0, 0, null)), "en");
        Assert.Contains("Best team: A — 80%", text);
        Assert.DoesNotContain("Lowest", text);
    }

    [Fact]
    public void Teams_without_exams_are_listed_and_a_long_list_is_cut()
    {
        var teams = new List<ExamReportTeamDto> { Team("A", 10, 8, 14, 80) };
        for (var i = 1; i <= 7; i++) teams.Add(Team("Quiet" + i, 5, 0, 0, null));
        var text = ExamReportMessage.Build(Report("week", teams.ToArray()), "en");

        Assert.Contains("No exams in the period: Quiet1, Quiet2, Quiet3, Quiet4, Quiet5 +2", text);
        Assert.Contains("for the week", text);
    }

    [Fact]
    public void Names_that_come_from_users_are_escaped_for_telegram_html()
    {
        var text = ExamReportMessage.Build(Report("month", Team("<b>A</b>", 3, 3, 3, 90)), "ru");
        Assert.Contains("&lt;5&gt;", text);
        Assert.Contains("&lt;b&gt;A&lt;/b&gt;", text);
        Assert.DoesNotContain("<b>A</b>", text);
    }

    [Theory]
    [InlineData("ru", "Отчёт по экзаменам")]
    [InlineData("uz", "imtihonlar hisoboti")]
    [InlineData("en", "Exam report")]
    public void The_title_is_in_the_language_of_the_chat(string lang, string expected)
        => Assert.Contains(expected, ExamReportMessage.Build(Report("term", Team("A", 3, 3, 3, 90)), lang));

    // ── the buttons ─────────────────────────────────────────────────────────
    [Fact]
    public void The_buttons_open_the_report_of_the_shown_period_first_and_the_other_two_after()
    {
        var json = JsonSerializer.Serialize(ExamReportMessage.Buttons("en", "https://jumbo.uz/", "TOKEN123", "month"));
        using var doc = JsonDocument.Parse(json);
        var rows = doc.RootElement.GetProperty("inline_keyboard").EnumerateArray().ToList();

        var open = rows[0].EnumerateArray().Single();
        Assert.Equal("https://jumbo.uz/r/TOKEN123/exams?period=month", open.GetProperty("url").GetString());

        var others = rows[1].EnumerateArray().Select(b => b.GetProperty("url").GetString()).ToList();
        Assert.Equal(new[] { "https://jumbo.uz/r/TOKEN123/exams?period=week", "https://jumbo.uz/r/TOKEN123/exams?period=term" }, others);
    }
}
