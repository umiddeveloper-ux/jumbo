using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Jumbo.Api.Data;
using Jumbo.Api.Services;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Jumbo.Tests;

// The director's exam report of a school by teams (api/reports/{token}/exams): the secret link, the teams and pupils it shows, the periods, the order,
// and that its numbers are the numbers of the teacher's own team exam report.
public class ExamReportTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(5);

    // ── the rig ─────────────────────────────────────────────────────────────
    private sealed class Rig
    {
        public int SchoolId;
        public string Token = "";
        public readonly List<TeamRig> Teams = new();
    }

    private sealed class TeamRig
    {
        public int ClassId;
        public string Name = "";
        public HttpClient TeacherClient = null!;
        public int TeacherId;
        public string TeacherName = "";
        public readonly List<(int UserId, string Name)> Pupils = new();
    }

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = api.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task WithDbAsync(Func<AppDbContext, Task> work) => WithDbAsync<int>(async db => { await work(db); return 0; });

    private static string Unique() => Guid.NewGuid().ToString("N")[..6];

    private async Task<Rig> NewSchoolAsync()
    {
        var rig = new Rig { Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=') };
        await WithDbAsync(async db =>
        {
            var school = new School { Region = "Test", Name = "School " + Unique(), IsVerified = true };
            db.Schools.Add(school);
            await db.SaveChangesAsync();
            rig.SchoolId = school.SchoolId;
            db.SchoolReportLinks.Add(new SchoolReportLink { SchoolId = school.SchoolId, Token = rig.Token, CreatedById = 1 });
            await db.SaveChangesAsync();
        });
        return rig;
    }

    // A team with its own teacher (a registered teacher account) and `pupils` approved pupils.
    private async Task<TeamRig> AddTeamAsync(Rig rig, string name, int pupils, bool archived = false)
    {
        var (teacherClient, teacherId, _, _) = await api.NewUserAsync(UserRole.Teacher);
        var team = new TeamRig { Name = name + " " + Unique(), TeacherClient = teacherClient, TeacherId = teacherId, TeacherName = "Teacher " + Unique() };
        await WithDbAsync(async db =>
        {
            var t = await db.Users.FindAsync(teacherId);
            t!.SchoolId = rig.SchoolId;
            t.FirstName = team.TeacherName.Split(' ')[0];
            t.LastName = team.TeacherName.Split(' ')[1];
            var cls = new SchoolClass { SchoolId = rig.SchoolId, TeacherId = teacherId, Name = team.Name, IsArchived = archived };
            db.SchoolClasses.Add(cls);
            await db.SaveChangesAsync();
            team.ClassId = cls.ClassId;
        });
        for (var i = 0; i < pupils; i++) team.Pupils.Add(await AddPupilAsync(rig, team, $"Pupil{i + 1}", approved: true));
        rig.Teams.Add(team);
        return team;
    }

    private async Task<(int UserId, string Name)> AddPupilAsync(Rig rig, TeamRig team, string first, bool approved)
    {
        var (_, userId, _, _) = await api.NewUserAsync();
        var last = "L" + Unique();
        await WithDbAsync(async db =>
        {
            var p = await db.Users.FindAsync(userId);
            p!.FirstName = first; p.LastName = last;
            p.SchoolId = rig.SchoolId; p.ClassId = team.ClassId; p.ClassApproved = approved;
            await db.SaveChangesAsync();
        });
        return (userId, $"{first} {last}");
    }

    // A teacher's exam (by code) of the team's teacher, started on `day` (app-day), with `questions` questions.
    private async Task<int> NewExamAsync(TeamRig teacher, DateOnly day, int questions = 20)
    {
        return await WithDbAsync(async db =>
        {
            var created = day.ToDateTime(new TimeOnly(10, 0)) - Offset;
            var exam = new TeacherExam
            {
                TeacherId = teacher.TeacherId, Code = Random.Shared.Next(100000, 1000000).ToString(), WordCount = questions,
                CreatedAt = DateTime.SpecifyKind(created, DateTimeKind.Utc), ExpiresAt = DateTime.SpecifyKind(created.AddMinutes(5), DateTimeKind.Utc)
            };
            db.TeacherExams.Add(exam);
            await db.SaveChangesAsync();
            return exam.TeacherExamId;
        });
    }

    private async Task AttemptAsync(TeamRig team, int userId, int? examId, int book, int correct, int total, DateTime takenAtUtc, int? classId = null)
    {
        await WithDbAsync(async db =>
        {
            db.ExamResults.Add(new ExamResult
            {
                UserId = userId, ClassId = classId ?? team.ClassId, BookNumber = book, TotalCount = total, CorrectCount = correct,
                TeacherExamId = examId, TakenAt = DateTime.SpecifyKind(takenAtUtc, DateTimeKind.Utc)
            });
            await db.SaveChangesAsync();
        });
    }

    // Noon of an app-day, as the UTC time stored in the database.
    private static DateTime Noon(DateOnly day) => day.ToDateTime(new TimeOnly(12, 0)) - Offset;

    private static DateOnly Today => DailyProgressService.Today;

    private async Task<SchoolExamReportDto> ReportAsync(Rig rig, string? period = null)
    {
        var url = $"/api/reports/{rig.Token}/exams" + (period is null ? "" : $"?period={period}");
        var response = await api.CreateClient().GetAsync(url);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SchoolExamReportDto>())!;
    }

    // ── the link ────────────────────────────────────────────────────────────
    [Fact]
    public async Task An_unknown_or_short_token_is_404()
    {
        var anon = api.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/reports/short/exams")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/reports/" + new string('x', 43) + "/exams")).StatusCode);
    }

    [Fact]
    public async Task A_revoked_token_is_404_and_a_live_one_is_not_cached_by_the_browser()
    {
        var rig = await NewSchoolAsync();
        var response = await api.CreateClient().GetAsync($"/api/reports/{rig.Token}/exams");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());

        await WithDbAsync(async db => await db.SchoolReportLinks.Where(l => l.Token == rig.Token)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.RevokedAt, DateTime.UtcNow)));
        Assert.Equal(HttpStatusCode.NotFound, (await api.CreateClient().GetAsync($"/api/reports/{rig.Token}/exams")).StatusCode);
    }

    // ── what the report shows ───────────────────────────────────────────────
    [Fact]
    public async Task Only_the_non_archived_teams_of_this_school_and_only_approved_pupils_are_shown_and_another_school_never_leaks()
    {
        var rig = await NewSchoolAsync();
        var a = await AddTeamAsync(rig, "A", 2);
        var archived = await AddTeamAsync(rig, "Old", 1, archived: true);
        var pending = await AddPupilAsync(rig, a, "Waiting", approved: false);

        var exam = await NewExamAsync(a, Today);
        await AttemptAsync(a, a.Pupils[0].UserId, exam, 1, 18, 20, Noon(Today));
        await AttemptAsync(a, pending.UserId, exam, 1, 20, 20, Noon(Today));                          // not approved: out
        await AttemptAsync(archived, archived.Pupils[0].UserId, await NewExamAsync(archived, Today), 1, 20, 20, Noon(Today));   // archived team: out

        // another school with a perfect exam of its own
        var other = await NewSchoolAsync();
        var o = await AddTeamAsync(other, "Other", 1);
        await AttemptAsync(o, o.Pupils[0].UserId, await NewExamAsync(o, Today), 1, 20, 20, Noon(Today));

        var report = await ReportAsync(rig);
        var team = Assert.Single(report.Teams);
        Assert.Equal(a.Name, team.TeamName);
        Assert.Equal(2, team.Pupils);                                            // the pending pupil is not counted
        Assert.Equal(2, team.PupilRows.Count);
        Assert.DoesNotContain(team.PupilRows, r => r.Name == pending.Name);
        Assert.Equal(1, team.Attempts);
        Assert.Equal(90, team.AvgPercent);
        Assert.Equal(1, report.Totals.Attempts);
        Assert.DoesNotContain(report.Teams, t => t.TeamName == o.Name);
    }

    [Fact]
    public async Task Teams_of_several_teachers_are_all_there_by_team_name_each_with_its_teacher()
    {
        var rig = await NewSchoolAsync();
        var b = await AddTeamAsync(rig, "B", 1);
        var a = await AddTeamAsync(rig, "A", 1);
        var report = await ReportAsync(rig);

        Assert.Equal(new[] { a.Name, b.Name }, report.Teams.Select(t => t.TeamName).ToArray());     // A before B
        Assert.Equal(a.TeacherName, report.Teams[0].TeacherName);
        Assert.Equal(b.TeacherName, report.Teams[1].TeacherName);
        Assert.Equal(2, report.Totals.Teams);
        Assert.Equal(2, report.Totals.Pupils);
        Assert.Equal(0, report.Totals.Participants);
        Assert.Null(report.Totals.AvgPercent);
    }

    [Fact]
    public async Task A_free_exam_a_moved_pupil_and_a_book_over_the_course_do_not_count()
    {
        var rig = await NewSchoolAsync();
        var a = await AddTeamAsync(rig, "A", 1);
        var b = await AddTeamAsync(rig, "B", 1);
        var exam = await NewExamAsync(a, Today);
        var pupil = a.Pupils[0].UserId;

        await AttemptAsync(a, pupil, null, 1, 20, 20, Noon(Today));                                  // a free exam: out
        await AttemptAsync(a, pupil, exam, 1, 10, 20, Noon(Today), classId: b.ClassId);              // taken as a member of another team: out
        await AttemptAsync(a, pupil, exam, 1, 0, 0, Noon(Today));                                    // no questions: out
        await AttemptAsync(a, pupil, exam, 1, 15, 20, Noon(Today));                                  // the one that counts

        var team = (await ReportAsync(rig)).Teams.Single(t => t.TeamName == a.Name);
        Assert.Equal(1, team.Attempts);
        Assert.Equal(75, team.PupilRows[0].BestPercent);
    }

    // ── the pupil table ─────────────────────────────────────────────────────
    [Fact]
    public async Task The_pupil_table_has_the_best_first_the_ones_without_an_exam_last_and_the_right_numbers()
    {
        var rig = await NewSchoolAsync();
        var a = await AddTeamAsync(rig, "A", 4);
        var (p1, p2, p3, p4) = (a.Pupils[0], a.Pupils[1], a.Pupils[2], a.Pupils[3]);
        var yesterday = Today.AddDays(-1);
        var e1 = await NewExamAsync(a, yesterday);
        var e2 = await NewExamAsync(a, Today);

        // p2: two attempts, best 90 % (yesterday), the last one 60 % (today)
        await AttemptAsync(a, p2.UserId, e1, 1, 18, 20, Noon(yesterday));
        await AttemptAsync(a, p2.UserId, e2, 1, 12, 20, Noon(Today));
        // p3: one attempt, 100 % in book 2
        await AttemptAsync(a, p3.UserId, e2, 2, 20, 20, Noon(Today));
        // p1: one attempt, 50 %; p4: none
        await AttemptAsync(a, p1.UserId, e2, 1, 10, 20, Noon(Today));

        var rows = (await ReportAsync(rig)).Teams.Single().PupilRows;
        Assert.Equal(new[] { p3.Name, p2.Name, p1.Name, p4.Name }, rows.Select(r => r.Name).ToArray());

        var r2 = rows.Single(r => r.Name == p2.Name);
        Assert.Equal(2, r2.Exams);
        Assert.Equal(90, r2.BestPercent);
        Assert.Equal(60, r2.LastPercent);
        Assert.Equal(Today, r2.LastDate);

        var r4 = rows.Single(r => r.Name == p4.Name);
        Assert.Equal(0, r4.Exams);
        Assert.Null(r4.BestPercent);
        Assert.Null(r4.LastPercent);
        Assert.Null(r4.LastDate);
        Assert.Equal(0, r4.WordsTotal);
    }

    [Fact]
    public async Task The_exams_of_a_team_list_the_day_the_teacher_the_questions_and_the_participants()
    {
        var rig = await NewSchoolAsync();
        var a = await AddTeamAsync(rig, "A", 3);
        var day = Today.AddDays(-2);
        var exam = await NewExamAsync(a, day, questions: 25);
        await AttemptAsync(a, a.Pupils[0].UserId, exam, 1, 20, 25, Noon(day));       // 80 %
        await AttemptAsync(a, a.Pupils[1].UserId, exam, 1, 15, 25, Noon(day));       // 60 %

        var held = (await ReportAsync(rig)).Teams.Single().Exams;
        var one = Assert.Single(held);
        Assert.Equal(day, one.Date);
        Assert.Equal(a.TeacherName, one.TeacherName);
        Assert.Equal(25, one.Questions);
        Assert.Equal(2, one.Participants);
        Assert.Equal(70, one.AvgPercent);
    }

    // ── periods ─────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("2026-11-27", "2026-09-01")]
    [InlineData("2027-03-10", "2026-09-01")]
    [InlineData("2026-09-01", "2026-09-01")]
    [InlineData("2026-08-31", "2025-09-01")]
    [InlineData("2027-01-01", "2026-09-01")]
    public void The_term_starts_on_the_first_of_september_of_the_school_year(string today, string from)
        => Assert.Equal(DateOnly.Parse(from), ExamReportPeriods.From("term", DateOnly.Parse(today)));

    [Theory]
    [InlineData("week", "week")]
    [InlineData("TERM", "term")]
    [InlineData("month", "month")]
    [InlineData(null, "month")]
    [InlineData("", "month")]
    [InlineData("year", "month")]
    public void The_period_key_falls_back_to_month(string? key, string expected)
        => Assert.Equal(expected, ExamReportPeriods.Normalize(key));

    [Fact]
    public void A_week_is_7_app_days_and_a_month_30_days_ending_today()
    {
        var today = new DateOnly(2026, 11, 27);
        Assert.Equal(new DateOnly(2026, 11, 21), ExamReportPeriods.From("week", today));
        Assert.Equal(new DateOnly(2026, 10, 29), ExamReportPeriods.From("month", today));
    }

    [Fact]
    public async Task The_week_and_the_month_include_their_first_day_and_leave_out_the_day_before()
    {
        var rig = await NewSchoolAsync();
        var a = await AddTeamAsync(rig, "A", 4);
        var exam = await NewExamAsync(a, Today);
        var today = Today;

        // week: the first day is today-6 (in), today-7 (out); month: the first day is today-29 (in), today-30 (out)
        await AttemptAsync(a, a.Pupils[0].UserId, exam, 1, 10, 10, Noon(today.AddDays(-6)));
        await AttemptAsync(a, a.Pupils[1].UserId, exam, 1, 10, 10, Noon(today.AddDays(-7)));
        await AttemptAsync(a, a.Pupils[2].UserId, exam, 1, 10, 10, Noon(today.AddDays(-29)));
        await AttemptAsync(a, a.Pupils[3].UserId, exam, 1, 10, 10, Noon(today.AddDays(-30)));

        Assert.Equal(1, (await ReportAsync(rig, "week")).Totals.Attempts);
        Assert.Equal(3, (await ReportAsync(rig, "month")).Totals.Attempts);
        Assert.Equal(3, (await ReportAsync(rig)).Totals.Attempts);                // month is the default
        // the school year started on 1 September: all four attempts are in it unless today is early September
        var term = ExamReportPeriods.From("term", today);
        var expected = new[] { -6, -7, -29, -30 }.Count(d => today.AddDays(d) >= term);
        Assert.Equal(expected, (await ReportAsync(rig, "term")).Totals.Attempts);
    }

    [Fact]
    public async Task The_day_changes_at_midnight_of_the_app_time_UTC_plus_5()
    {
        var rig = await NewSchoolAsync();
        var a = await AddTeamAsync(rig, "A", 2);
        var exam = await NewExamAsync(a, Today);
        var firstDay = ExamReportPeriods.From("week", Today);

        // 00:00 app time of the first day = 19:00 UTC of the day before: in. One minute earlier: out.
        var midnight = firstDay.ToDateTime(TimeOnly.MinValue) - Offset;
        await AttemptAsync(a, a.Pupils[0].UserId, exam, 1, 10, 10, midnight);
        await AttemptAsync(a, a.Pupils[1].UserId, exam, 1, 10, 10, midnight.AddMinutes(-1));

        var team = (await ReportAsync(rig, "week")).Teams.Single();
        Assert.Equal(1, team.Attempts);
        Assert.Equal(1, team.PupilRows.Count(r => r.Exams > 0));
    }

    // ── totals ──────────────────────────────────────────────────────────────
    [Fact]
    public async Task The_totals_are_the_sums_of_the_teams_and_an_exam_of_two_teams_is_one_exam_held()
    {
        var rig = await NewSchoolAsync();
        var a = await AddTeamAsync(rig, "A", 3);
        var b = await AddTeamAsync(rig, "B", 2);

        var shared = await NewExamAsync(a, Today);                       // one exam by code, pupils of both teams take it
        var onlyB = await NewExamAsync(b, Today.AddDays(-3));
        await AttemptAsync(a, a.Pupils[0].UserId, shared, 1, 20, 20, Noon(Today));    // 100
        await AttemptAsync(a, a.Pupils[1].UserId, shared, 1, 10, 20, Noon(Today));    // 50
        await AttemptAsync(b, b.Pupils[0].UserId, shared, 1, 15, 20, Noon(Today));    // 75
        await AttemptAsync(b, b.Pupils[1].UserId, onlyB, 1, 7, 20, Noon(Today.AddDays(-3)));   // 35

        var report = await ReportAsync(rig);
        var totals = report.Totals;

        Assert.Equal(report.Teams.Sum(t => t.Pupils), totals.Pupils);
        Assert.Equal(report.Teams.Sum(t => t.Participants), totals.Participants);
        Assert.Equal(report.Teams.Sum(t => t.Attempts), totals.Attempts);
        Assert.Equal(4, totals.Attempts);
        Assert.Equal(4, totals.Participants);
        Assert.Equal(5, totals.Pupils);
        Assert.Equal(2, totals.ExamsHeld);                                              // not 3: the shared exam is one exam
        Assert.Equal(3, report.Teams.Sum(t => t.Exams.Count));                          // ... but one in each team's list
        Assert.Equal(65, totals.AvgPercent);                                            // (100 + 50 + 75 + 35) / 4 = 65
        Assert.Equal(75, report.Teams.Single(t => t.TeamName == a.Name).AvgPercent);    // (100 + 50) / 2
        Assert.Equal(55, report.Teams.Single(t => t.TeamName == b.Name).AvgPercent);    // (75 + 35) / 2
    }

    // ── the numbers are the teacher's numbers ───────────────────────────────
    [Fact]
    public async Task The_words_learned_are_exactly_the_numbers_of_the_teachers_own_team_exam_report()
    {
        var rig = await NewSchoolAsync();
        var a = await AddTeamAsync(rig, "A", 3);
        var exam = await NewExamAsync(a, Today);
        var old = await NewExamAsync(a, Today.AddDays(-200));            // long ago: outside the period, still counted in the words (all time)

        await AttemptAsync(a, a.Pupils[0].UserId, exam, 1, 17, 20, Noon(Today));                   // book 1: 85 %
        await AttemptAsync(a, a.Pupils[0].UserId, old, 1, 20, 20, Noon(Today.AddDays(-200)));      // book 1: 100 % (the best of the book)
        await AttemptAsync(a, a.Pupils[0].UserId, exam, 3, 7, 20, Noon(Today));                    // book 3: 35 %
        await AttemptAsync(a, a.Pupils[1].UserId, exam, 2, 13, 20, Noon(Today));                   // book 2: 65 %

        var report = (await ReportAsync(rig)).Teams.Single();

        var teacher = await a.TeacherClient.GetFromJsonAsync<ExamTeamReportDto>($"/api/teacher/exams/teams/{a.ClassId}");
        Assert.NotNull(teacher);
        Assert.Equal(a.Pupils.Count, teacher.Pupils.Count);
        foreach (var row in teacher.Pupils)
        {
            var director = report.PupilRows.Single(r => r.Name == row.Name);
            Assert.Equal(row.Words.Sum(w => w ?? 0), director.WordsTotal);
        }
        // by hand for the first pupil: 300 x 100 % + 300 x 35 % = 300 + 105
        Assert.Equal(405, report.PupilRows.Single(r => r.Name == a.Pupils[0].Name).WordsTotal);
        Assert.Equal(195, report.PupilRows.Single(r => r.Name == a.Pupils[1].Name).WordsTotal);
        Assert.Equal(0, report.PupilRows.Single(r => r.Name == a.Pupils[2].Name).WordsTotal);
    }
}
