using System.Net;
using System.Net.Http.Json;
using Jumbo.Api.Data;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Jumbo.Tests;

// The two percents of the welcome-back dialog: never 0 or 100 by a rounding accident.
public class LearningPercentTests
{
    [Theory]
    [InlineData(0, 3000, 0)]
    [InlineData(1, 3000, 1)]            // one word is 0.03 %: still "1", the learner sees that something is done
    [InlineData(15, 3000, 1)]           // 0.5 % rounds up
    [InlineData(44, 3000, 1)]           // 1.47 % rounds to 1
    [InlineData(45, 3000, 2)]           // 1.5 % rounds away from zero, as the home page label does
    [InlineData(1500, 3000, 50)]
    [InlineData(2999, 3000, 99)]        // never 100 until everything is learned
    [InlineData(3000, 3000, 100)]
    [InlineData(3001, 3000, 100)]       // (a word of an older edition of the book)
    [InlineData(5, 0, 0)]               // a course without words
    [InlineData(-1, 3000, 0)]
    public void The_course_percent(int learned, int total, int expected) => Assert.Equal(expected, LearningPercent.Course(learned, total));

    [Theory]
    [InlineData(0, 60, 0)]
    [InlineData(1, 60, 1)]
    [InlineData(1, 600, 1)]             // below one percent: still "1"
    [InlineData(4, 60, 6)]              // 6.67 % is rounded down, as the book card of the home page does
    [InlineData(30, 60, 50)]
    [InlineData(59, 60, 98)]
    [InlineData(60, 60, 100)]
    [InlineData(61, 60, 100)]
    [InlineData(3, 0, 0)]
    public void The_book_percent(int partsDone, int partsTotal, int expected) => Assert.Equal(expected, LearningPercent.Book(partsDone, partsTotal));
}

// GET api/progress/summary: the numbers behind the welcome-back dialog. The test database has no books of its own, so the tests make their
// topics in book 1 and the words in Turkish (language 4, which no other test fills) where an exact number is checked.
[Collection("subjects")]
public class LearningSummaryApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private const int Turkish = 4;

    private record Made(int[] Subjects, int[] Words);

    // 3 topics of book 1 with 2 + 2 + 1 words of the language.
    private async Task<Made> MakeCourseAsync(int languageId)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var picture = new SvgImage { SVG = "<svg viewBox='0 0 1 1' xmlns='http://www.w3.org/2000/svg'/>" };
        db.SvgImages.Add(picture);
        await db.SaveChangesAsync();

        var subjects = Enumerable.Range(1, 3).Select(i => new Subject { BookId = 1, SubjectName = $"summary-{Guid.NewGuid():N}"[..20], SvgImageId = picture.SvgImageId, SequenceNumber = i }).ToList();
        db.Subjects.AddRange(subjects);
        await db.SaveChangesAsync();

        var words = new List<Word>();
        var perTopic = new[] { 2, 2, 1 };
        for (var i = 0; i < subjects.Count; i++)
            for (var k = 0; k < perTopic[i]; k++)
                words.Add(new Word { SubjectId = subjects[i].SubjectId, LanguageId = languageId, SvgImageId = picture.SvgImageId, WordName = $"w{i}{k}", FirstSymbol = "W", WordNumber = 1 });
        db.Words.AddRange(words);
        await db.SaveChangesAsync();
        return new Made(subjects.Select(s => s.SubjectId).ToArray(), words.Select(w => w.WordId).ToArray());
    }

    private static Task<HttpResponseMessage> LearnAsync(HttpClient client, params int[] wordIds)
        => client.PostAsJsonAsync("/api/progress/words/ensure", new EnsureWordsRequest { WordIds = wordIds.ToList() });

    private static async Task<LearningSummaryDto> SummaryAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/progress/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LearningSummaryDto>())!;
    }

    [Fact]
    public async Task A_new_learner_has_not_started()
    {
        var (client, _, _, _) = await api.NewUserAsync();
        var s = await SummaryAsync(client);
        Assert.False(s.HasStarted);
        Assert.Equal(0, s.LearnedWords);
        Assert.Equal(0, s.CoursePercent);
        Assert.Equal(1, s.StudyLanguageId);
        Assert.Equal("en", s.StudyLanguageCode);
    }

    [Fact]
    public async Task After_learning_words_the_numbers_are_the_ones_the_home_page_shows()
    {
        var course = await MakeCourseAsync(Turkish);
        var (client, _, _, _) = await api.NewUserAsync(studyLanguageId: Turkish);
        Assert.True((await LearnAsync(client, course.Words[0], course.Words[1])).IsSuccessStatusCode);

        var s = await SummaryAsync(client);
        var total = await api.CreateClient().GetFromJsonAsync<int>($"/api/words/count?languageId={Turkish}");   // what the home page divides by
        var learned = (await client.GetFromJsonAsync<List<UsersWord>>("/api/progress/words"))!.Count;           // what the home page counts

        Assert.True(s.HasStarted);
        Assert.Equal("tr", s.StudyLanguageCode);
        Assert.Equal(2, s.LearnedWords);
        Assert.Equal(learned, s.LearnedWords);
        Assert.Equal(total, s.TotalWords);
        Assert.Equal(LearningPercent.Course(learned, total), s.CoursePercent);
        Assert.InRange(s.CoursePercent, 1, 99);
        Assert.Equal(1, s.CurrentBook);
        Assert.Equal("A1", s.Level);
        Assert.Equal(await client.GetFromJsonAsync<int>("/api/progress/current-book"), s.CurrentBook);   // the same book as the rest of the product
    }

    [Fact]
    public async Task The_book_percent_counts_the_three_games_of_every_topic()
    {
        var course = await MakeCourseAsync(Turkish);
        var (client, userId, _, _) = await api.NewUserAsync(studyLanguageId: Turkish);
        Assert.True((await LearnAsync(client, course.Words[0])).IsSuccessStatusCode);

        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // topic 1: all three games won; topic 2: one game
            db.UserSubjectProgresses.Add(new UserSubjectProgress { UserId = userId, SubjectId = course.Subjects[0], FindMeWon = true, ListenWon = true, PhraseWon = true });
            db.UserSubjectProgresses.Add(new UserSubjectProgress { UserId = userId, SubjectId = course.Subjects[1], FindMeWon = true });
            await db.SaveChangesAsync();
        }

        var s = await SummaryAsync(client);
        int topics;
        using (var scope = api.Services.CreateScope())
            topics = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Subjects.CountAsync(x => x.BookId == 1);

        Assert.Equal(1, s.CurrentBook);
        Assert.Equal(topics, s.BookTopics);
        Assert.Equal(1, s.BookTopicsPassed);
        Assert.Equal(LearningPercent.Book(4, topics * UserSubjectProgress.Parts), s.BookPercent);
        Assert.InRange(s.BookPercent, 1, 99);
    }

    [Fact]
    public async Task Words_of_another_language_do_not_count()
    {
        // an English word learned by a learner who studies Turkish: not started in Turkish
        var english = await MakeCourseAsync(1);
        var (client, _, _, _) = await api.NewUserAsync(studyLanguageId: Turkish);
        Assert.True((await LearnAsync(client, english.Words[0])).IsSuccessStatusCode);

        var s = await SummaryAsync(client);
        Assert.False(s.HasStarted);
        Assert.Equal(0, s.LearnedWords);
        Assert.Equal(0, s.CoursePercent);
    }

    [Fact]
    public async Task A_learner_of_english_gets_the_english_numbers()
    {
        var english = await MakeCourseAsync(1);
        var (client, _, _, _) = await api.NewUserAsync();
        Assert.True((await LearnAsync(client, english.Words[0], english.Words[2], english.Words[4])).IsSuccessStatusCode);

        var s = await SummaryAsync(client);
        var total = await api.CreateClient().GetFromJsonAsync<int>("/api/words/count?languageId=1");
        Assert.True(s.HasStarted);
        Assert.Equal("en", s.StudyLanguageCode);
        Assert.Equal(3, s.LearnedWords);
        Assert.InRange(s.TotalWords, 5, total + 50);        // other tests add English words at the same time: the same quantity, not necessarily the same instant
        Assert.Equal(LearningPercent.Course(3, s.TotalWords), s.CoursePercent);
    }

    [Theory]
    [InlineData(UserRole.Teacher)]
    [InlineData(UserRole.Director)]
    [InlineData(UserRole.Admin)]
    public async Task Teachers_directors_and_the_admin_get_204(UserRole role)
    {
        var (client, _, _, _) = await api.NewUserAsync(role);
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/progress/summary")).StatusCode);
    }

    [Fact]
    public async Task Without_a_token_it_is_401()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().GetAsync("/api/progress/summary")).StatusCode);

    [Fact]
    public async Task The_answer_is_not_cached()
    {
        var (client, _, _, _) = await api.NewUserAsync();
        var response = await client.GetAsync("/api/progress/summary");
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty);
    }
}
