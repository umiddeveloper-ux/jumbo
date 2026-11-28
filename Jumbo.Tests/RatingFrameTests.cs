using System.Net.Http.Json;
using Jumbo.Api.Data;
using Jumbo.Api.Services;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Jumbo.Tests;

// The leaderboard entries carry the book the player is in and the version in force (the site draws the frame of the avatar in the colour of the
// book and the star of the version from them). Old fields and the order are unchanged.
public class RatingFrameTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private record Seeded(HttpClient Client, int UserId, string Name);

    // A learner with a name of his own (the display name is "Q1a2b3c4d U."), words this week, books 1..books opened, and a version.
    private async Task<Seeded> SeedAsync(int words, int unlockedUpTo, string? plan = null, DateTime? planUntil = null,
                                         UserRole role = UserRole.Learner, bool hideName = false)
    {
        var (client, userId, _, _) = await api.NewUserAsync(role);
        var first = "Q" + Guid.NewGuid().ToString("N")[..8];
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.UserId == userId);
        user.FirstName = first;
        user.LastName = "User";
        user.HideName = hideName;
        user.Plan = plan ?? Plans.Bronze;
        user.PlanUntil = planUntil;
        for (var b = 2; b <= unlockedUpTo; b++) db.UserBookUnlocks.Add(new UserBookUnlock { UserId = userId, BookNumber = b });
        // "in book N" = the books before it are finished: every topic of a lower book that exists in the test database is won
        // (the test database also holds topics that other tests made in book 1, so opening a book alone does not move the learner)
        var done = await db.Subjects.AsNoTracking().Where(s => s.BookId < unlockedUpTo).Select(s => s.SubjectId).ToListAsync();
        foreach (var subjectId in done)
            db.UserSubjectProgresses.Add(new UserSubjectProgress { UserId = userId, SubjectId = subjectId, FindMeWon = true, ListenWon = true, PhraseWon = true });
        db.UserDailyProgresses.Add(new UserDailyProgress { UserId = userId, Date = DailyProgressService.Today, WordsCount = words });
        await db.SaveChangesAsync();
        return new Seeded(client, userId, first + " U.");
    }

    // Words this week of the test learners: every call is higher than the one before (and than the runs before: the test database keeps its users), so the learners of
    // a test are always near the top of the 100 the list shows.
    private static int _seq;
    private static int NewBase() => (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1_790_000_000) * 50 + Interlocked.Add(ref _seq, 10);

    private static async Task<RatingDto> ListAsync(HttpClient client)
        => (await client.GetFromJsonAsync<RatingDto>("/api/rating?scope=all&period=week&take=100"))!;

    [Fact]
    public async Task An_entry_carries_the_book_and_the_version_in_force()
    {
        var w = NewBase();
        var silver = await SeedAsync(w + 1, unlockedUpTo: 4, Plans.Silver, DateTime.UtcNow.AddDays(10));
        var plain = await SeedAsync(w, unlockedUpTo: 1);

        var list = await ListAsync(plain.Client);
        var s = list.Entries.Single(e => e.Name == silver.Name);
        var p = list.Entries.Single(e => e.Name == plain.Name);

        Assert.Equal(4, s.CurrentBook);               // the highest book opened is the one he is in (no topic is won in the test data)
        Assert.Equal(Plans.Silver, s.Plan);
        Assert.Equal(1, p.CurrentBook);
        Assert.Equal(Plans.Bronze, p.Plan);
    }

    [Fact]
    public async Task A_paid_version_that_has_run_out_shows_as_bronze()
    {
        var expired = await SeedAsync(NewBase(), unlockedUpTo: 2, Plans.Gold, DateTime.UtcNow.AddDays(-1));
        var list = await ListAsync(expired.Client);
        Assert.Equal(Plans.Bronze, list.Entries.Single(e => e.Name == expired.Name).Plan);
    }

    [Fact]
    public async Task The_own_place_carries_the_same_data()
    {
        var me = await SeedAsync(NewBase(), unlockedUpTo: 3, Plans.Gold, DateTime.UtcNow.AddDays(30));
        var list = await ListAsync(me.Client);
        Assert.NotNull(list.Me);
        Assert.True(list.Me!.IsMe);
        Assert.Equal(3, list.Me.CurrentBook);
        Assert.Equal(Plans.Gold, list.Me.Plan);
        Assert.Equal(list.Entries.Single(e => e.Name == me.Name).CurrentBook, list.Me.CurrentBook);
    }

    [Fact]
    public async Task A_hidden_name_shows_neither_the_book_nor_the_version()
    {
        var hiddenWords = NewBase() + 1;
        await SeedAsync(hiddenWords, unlockedUpTo: 5, Plans.Silver, DateTime.UtcNow.AddDays(5), hideName: true);
        var viewer = await SeedAsync(hiddenWords - 1, unlockedUpTo: 1);
        var list = await ListAsync(viewer.Client);
        var entry = list.Entries.Single(e => e.Name is null && e.Words == hiddenWords);
        Assert.Equal(0, entry.CurrentBook);
        Assert.Null(entry.Plan);
    }

    [Fact]
    public async Task A_teacher_in_the_list_gets_no_frame_and_no_star()
    {
        var teacher = await SeedAsync(NewBase() + 1, unlockedUpTo: 4, Plans.Gold, DateTime.UtcNow.AddDays(5), UserRole.Teacher);
        var viewer = await SeedAsync(NewBase(), unlockedUpTo: 1);
        var entry = (await ListAsync(viewer.Client)).Entries.Single(e => e.Name == teacher.Name);
        Assert.Equal(0, entry.CurrentBook);
        Assert.Null(entry.Plan);
    }

    [Fact]
    public async Task The_old_fields_and_the_order_are_unchanged()
    {
        var w = NewBase();
        var a = await SeedAsync(w + 3, unlockedUpTo: 3);
        var b = await SeedAsync(w + 2, unlockedUpTo: 1, Plans.Silver, DateTime.UtcNow.AddDays(5));
        var c = await SeedAsync(w + 1, unlockedUpTo: 2);

        var entries = (await ListAsync(c.Client)).Entries;
        var ours = entries.Where(e => new[] { a.Name, b.Name, c.Name }.Contains(e.Name)).ToList();
        Assert.Equal(new[] { a.Name, b.Name, c.Name }, ours.Select(e => e.Name).ToArray());   // by words, high first
        Assert.Equal(new[] { w + 3, w + 2, w + 1 }, ours.Select(e => e.Words).ToArray());
        Assert.True(ours[0].Rank < ours[1].Rank && ours[1].Rank < ours[2].Rank);
        Assert.Equal(entries.Select(e => e.Rank).OrderBy(r => r), entries.Select(e => e.Rank));  // the places still go up the list
    }

    [Fact]
    public async Task One_rule_for_a_single_learner_and_for_many()
    {
        var one = await SeedAsync(1, unlockedUpTo: 1);
        var three = await SeedAsync(1, unlockedUpTo: 3);
        var six = await SeedAsync(1, unlockedUpTo: 6);

        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var many = await BookProgress.CurrentBooksAsync(db, [one.UserId, three.UserId, six.UserId]);

        Assert.Equal(1, many[one.UserId]);
        Assert.Equal(3, many[three.UserId]);
        Assert.Equal(6, many[six.UserId]);
        foreach (var s in new[] { one, three, six })
            Assert.Equal(many[s.UserId], await BookProgress.CurrentBookAsync(db, s.UserId));
        Assert.Empty(await BookProgress.CurrentBooksAsync(db, []));
    }

    [Fact]
    public async Task The_teachers_view_of_the_pupils_has_the_book_and_the_version_too()
    {
        var silver = await SeedAsync(7, unlockedUpTo: 2, Plans.Silver, DateTime.UtcNow.AddDays(9));
        var plain = await SeedAsync(3, unlockedUpTo: 1);

        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dto = await new RatingService(db).GetForPupilsAsync([silver.UserId, plain.UserId], "week");

        var s = dto.Entries.Single(e => e.Name!.StartsWith(silver.Name[..9]));
        var p = dto.Entries.Single(e => e.Name!.StartsWith(plain.Name[..9]));
        Assert.Equal((2, Plans.Silver), (s.CurrentBook, s.Plan));
        Assert.Equal((1, Plans.Bronze), (p.CurrentBook, p.Plan));
    }
}
