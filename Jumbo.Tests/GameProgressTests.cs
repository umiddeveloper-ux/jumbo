using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Jumbo.Api.Data;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Jumbo.Tests;

public class GameProgressRulesTests
{
    [Theory]
    [InlineData("{\"v\":1}", true)]
    [InlineData("{\"v\":1,\"words\":[1,2,3]}", true)]
    [InlineData("{\"v\":100}", true)]
    [InlineData("{\"v\":0}", false)]
    [InlineData("{\"v\":101}", false)]
    [InlineData("{\"v\":\"1\"}", false)]
    [InlineData("{\"words\":[1]}", false)]
    [InlineData("[1,2]", false)]
    [InlineData("\"text\"", false)]
    [InlineData("not json", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void A_state_is_a_small_json_object_with_a_version(string? state, bool valid)
        => Assert.Equal(valid, GameProgressRules.IsValidState(state));

    [Fact]
    public void A_state_over_32_kb_is_refused()
    {
        var big = "{\"v\":1,\"x\":\"" + new string('a', GameProgressRules.MaxStateBytes) + "\"}";
        Assert.False(GameProgressRules.IsValidState(big));
    }

    [Theory]
    [InlineData(0, 15, true)]
    [InlineData(4, 15, true)]
    [InlineData(15, 15, true)]
    [InlineData(16, 15, false)]
    [InlineData(-1, 15, false)]
    [InlineData(0, 0, false)]
    [InlineData(1, 201, false)]
    public void The_progress_is_between_nothing_and_the_total(int done, int total, bool valid)
        => Assert.Equal(valid, GameProgressRules.IsValidProgress(done, total));

    [Theory]
    [InlineData("FindMe", "findme")]
    [InlineData("ListenAndFind", "listen")]
    [InlineData("Phrase", "phrase")]
    [InlineData("Sequence", "sequence")]
    [InlineData("Steps", "steps")]
    [InlineData("Other", null)]
    [InlineData(null, null)]
    public void The_win_of_a_game_maps_to_its_saved_game(string? gameType, string? key)
        => Assert.Equal(key, GameProgressRules.KeyOfGameType(gameType));

    [Fact]
    public void Every_game_key_is_the_name_in_the_address_of_the_game()
        => Assert.Equal(new[] { "findme", "listen", "phrase", "sequence", "steps" }, GameProgressRules.Games);

    [Fact]
    public void A_saved_game_expires_after_two_weeks()
    {
        var now = new DateTime(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(now.AddDays(-14), GameProgressRules.ExpiresBefore(now));
    }
}

// The games a learner left half way are kept on the server (one saved game per learner, game and topic) so that he can go on at the next visit.
public class GameProgressApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private async Task<int> NewTopicAsync()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var picture = new SvgImage { SVG = "<svg viewBox='0 0 1 1' xmlns='http://www.w3.org/2000/svg'/>" };
        db.SvgImages.Add(picture);
        await db.SaveChangesAsync();
        var subject = new Subject { BookId = 98, SubjectName = "games-" + Guid.NewGuid().ToString("N")[..8], SvgImageId = picture.SvgImageId, SequenceNumber = 1 };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync();
        return subject.SubjectId;
    }

    private static SaveGameProgressRequest Save(string game, int subjectId, int done = 4, int total = 15, string? state = null) => new()
    {
        Game = game, SubjectId = subjectId, Done = done, Total = total, State = state ?? $"{{\"v\":1,\"done\":{done}}}"
    };

    private async Task<int> RowsAsync(int userId, string? game = null)
    {
        using var scope = api.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().GameProgresses
            .CountAsync(g => g.UserId == userId && (game == null || g.Game == game));
    }

    [Fact]
    public async Task Saving_twice_keeps_one_row_with_the_latest_state()
    {
        var (client, userId, _, _) = await api.NewUserAsync();
        var topic = await NewTopicAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/games/progress", Save("findme", topic, done: 4))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/games/progress", Save("findme", topic, done: 5))).StatusCode);

        Assert.Equal(1, await RowsAsync(userId));
        var got = await client.GetFromJsonAsync<GameProgressDto>($"/api/games/progress?game=findme&subjectId={topic}");
        Assert.NotNull(got);
        Assert.Equal(5, got.Done);
        Assert.Equal(15, got.Total);
        Assert.Equal("{\"v\":1,\"done\":5}", got.State);
    }

    [Fact]
    public async Task Each_game_of_a_topic_has_its_own_saved_game()
    {
        var (client, userId, _, _) = await api.NewUserAsync();
        var topic = await NewTopicAsync();
        foreach (var game in GameProgressRules.Games)
            Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/games/progress", Save(game, topic))).StatusCode);
        Assert.Equal(5, await RowsAsync(userId));
    }

    [Fact]
    public async Task Nothing_saved_answers_204()
    {
        var (client, _, _, _) = await api.NewUserAsync();
        var topic = await NewTopicAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync($"/api/games/progress?game=listen&subjectId={topic}")).StatusCode);
    }

    [Fact]
    public async Task The_unfinished_list_has_no_state_and_the_newest_first()
    {
        var (client, _, _, _) = await api.NewUserAsync();
        var a = await NewTopicAsync();
        var b = await NewTopicAsync();
        await client.PutAsJsonAsync("/api/games/progress", Save("findme", a, done: 3));
        await Task.Delay(30);
        await client.PutAsJsonAsync("/api/games/progress", Save("steps", b, done: 7, total: 15));

        var json = await client.GetStringAsync("/api/games/progress/unfinished");
        Assert.DoesNotContain("\"state\"", json, StringComparison.OrdinalIgnoreCase);
        var list = JsonSerializer.Deserialize<List<GameProgressSummaryDto>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(2, list.Count);
        Assert.Equal(("steps", b, 7, 15), (list[0].Game, list[0].SubjectId, list[0].Done, list[0].Total));
        Assert.Equal(("findme", a, 3), (list[1].Game, list[1].SubjectId, list[1].Done));
    }

    [Fact]
    public async Task Deleting_removes_the_saved_game()
    {
        var (client, userId, _, _) = await api.NewUserAsync();
        var topic = await NewTopicAsync();
        await client.PutAsJsonAsync("/api/games/progress", Save("phrase", topic));
        Assert.Equal(1, await RowsAsync(userId));

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/games/progress?game=phrase&subjectId={topic}")).StatusCode);
        Assert.Equal(0, await RowsAsync(userId));
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync($"/api/games/progress?game=phrase&subjectId={topic}")).StatusCode);
    }

    [Fact]
    public async Task A_saved_game_older_than_two_weeks_is_not_given_and_is_removed()
    {
        var (client, userId, _, _) = await api.NewUserAsync();
        var topic = await NewTopicAsync();
        await client.PutAsJsonAsync("/api/games/progress", Save("sequence", topic, done: 2, total: 4));

        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.GameProgresses.Where(g => g.UserId == userId).ExecuteUpdateAsync(s => s.SetProperty(g => g.UpdatedAt, DateTime.UtcNow.AddDays(-15)));
        }

        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync($"/api/games/progress?game=sequence&subjectId={topic}")).StatusCode);
        var list = await client.GetFromJsonAsync<List<GameProgressSummaryDto>>("/api/games/progress/unfinished");
        Assert.Empty(list!);
        Assert.Equal(0, await RowsAsync(userId));         // the list removed it
    }

    [Fact]
    public async Task A_saved_game_of_thirteen_days_is_still_there()
    {
        var (client, userId, _, _) = await api.NewUserAsync();
        var topic = await NewTopicAsync();
        await client.PutAsJsonAsync("/api/games/progress", Save("steps", topic));
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.GameProgresses.Where(g => g.UserId == userId).ExecuteUpdateAsync(s => s.SetProperty(g => g.UpdatedAt, DateTime.UtcNow.AddDays(-13)));
        }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/games/progress?game=steps&subjectId={topic}")).StatusCode);
    }

    [Theory]
    [InlineData(UserRole.Teacher)]
    [InlineData(UserRole.Director)]
    [InlineData(UserRole.Admin)]
    public async Task Nothing_is_stored_for_a_teacher_a_director_or_the_admin(UserRole role)
    {
        var (client, userId, _, _) = await api.NewUserAsync(role);
        var topic = await NewTopicAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/games/progress", Save("findme", topic))).StatusCode);
        Assert.Equal(0, await RowsAsync(userId));
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync($"/api/games/progress?game=findme&subjectId={topic}")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<GameProgressSummaryDto>>("/api/games/progress/unfinished"))!);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/games/progress?game=findme&subjectId={topic}")).StatusCode);
    }

    [Fact]
    public async Task Another_learner_neither_reads_nor_overwrites_a_saved_game()
    {
        var (alice, aliceId, _, _) = await api.NewUserAsync();
        var (bob, bobId, _, _) = await api.NewUserAsync();
        var topic = await NewTopicAsync();

        await alice.PutAsJsonAsync("/api/games/progress", Save("findme", topic, done: 6, state: "{\"v\":1,\"owner\":\"alice\"}"));

        Assert.Equal(HttpStatusCode.NoContent, (await bob.GetAsync($"/api/games/progress?game=findme&subjectId={topic}")).StatusCode);
        Assert.Empty((await bob.GetFromJsonAsync<List<GameProgressSummaryDto>>("/api/games/progress/unfinished"))!);

        // bob saves the same game of the same topic: his own row, alice's stays
        await bob.PutAsJsonAsync("/api/games/progress", Save("findme", topic, done: 1, state: "{\"v\":1,\"owner\":\"bob\"}"));
        // and his delete does not touch alice's
        await bob.DeleteAsync($"/api/games/progress?game=findme&subjectId={topic}");

        var mine = await alice.GetFromJsonAsync<GameProgressDto>($"/api/games/progress?game=findme&subjectId={topic}");
        Assert.Equal(6, mine!.Done);
        Assert.Contains("alice", mine.State);
        Assert.Equal(1, await RowsAsync(aliceId));
        Assert.Equal(0, await RowsAsync(bobId));
    }

    [Fact]
    public async Task Without_a_token_it_is_401()
    {
        var anon = api.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PutAsJsonAsync("/api/games/progress", Save("findme", 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/games/progress/unfinished")).StatusCode);
    }

    [Fact]
    public async Task Bad_requests_are_refused_and_nothing_is_stored()
    {
        var (client, userId, _, _) = await api.NewUserAsync();
        var topic = await NewTopicAsync();
        var big = "{\"v\":1,\"x\":\"" + new string('a', GameProgressRules.MaxStateBytes) + "\"}";

        var bad = new[]
        {
            Save("chess", topic),                                 // unknown game
            Save("findme", topic, done: 16, total: 15),           // done above total
            Save("findme", topic, done: 0, total: 0),             // no total
            Save("findme", topic, done: 1, total: 201),           // absurd total
            Save("findme", topic, state: big),                    // too big
            Save("findme", topic, state: "not json"),             // not json
            Save("findme", topic, state: "{\"words\":[1]}"),      // no version
            Save("findme", 0),                                    // no topic
            Save("findme", 2_000_000_000),                        // no such topic
        };
        foreach (var request in bad)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/games/progress", request)).StatusCode);

        Assert.Equal(0, await RowsAsync(userId));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/games/progress?game=chess&subjectId={topic}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/api/games/progress?game=chess&subjectId={topic}")).StatusCode);
    }

    [Fact]
    public async Task Deleting_the_account_removes_the_saved_games()
    {
        var (client, userId, _, password) = await api.NewUserAsync();
        var topic = await NewTopicAsync();
        await client.PutAsJsonAsync("/api/games/progress", Save("findme", topic));
        await client.PutAsJsonAsync("/api/games/progress", Save("steps", topic));
        Assert.Equal(2, await RowsAsync(userId));

        var response = await client.PostAsJsonAsync("/api/users/me/delete", new DeleteAccountRequest { CurrentPassword = password });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        Assert.Equal(0, await RowsAsync(userId));
    }

    [Fact]
    public async Task Many_saves_at_once_for_one_game_leave_one_row()
    {
        var (client, userId, _, _) = await api.NewUserAsync();
        var topic = await NewTopicAsync();

        var results = await Task.WhenAll(Enumerable.Range(1, 12).Select(i =>
            client.PutAsJsonAsync("/api/games/progress", Save("listen", topic, done: i % 15, state: $"{{\"v\":1,\"i\":{i}}}"))));
        Assert.All(results, r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));
        Assert.Equal(1, await RowsAsync(userId));
    }

    [Fact]
    public async Task Winning_the_game_removes_its_saved_game_only()
    {
        var (client, userId, _, _) = await api.NewUserAsync();
        var topic = await NewTopicAsync();
        await client.PutAsJsonAsync("/api/games/progress", Save("findme", topic));
        await client.PutAsJsonAsync("/api/games/progress", Save("steps", topic));

        var win = await client.PostAsJsonAsync($"/api/progress/subjects/{topic}/win", new MarkGameWonRequest { GameType = "FindMe" });
        Assert.True(win.IsSuccessStatusCode, await win.Content.ReadAsStringAsync());

        Assert.Equal(0, await RowsAsync(userId, "findme"));
        Assert.Equal(1, await RowsAsync(userId, "steps"));       // another game of the topic is still half way
    }
}

// What the games write into a saved game must come back the same (and an unknown version or broken text must not).
public class GameStatesTests
{
    [Fact]
    public void Find_me_state_comes_back_the_same_and_is_a_valid_state_for_the_server()
    {
        var state = new FindMeState { Lang = 1, Shown = [1, 2, 3, 4, 5, 6], Unseen = [7, 8], Matched = [], Target = 3, Found = 4, Words = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10] };
        var json = GameStates.Write(state);
        Assert.True(GameProgressRules.IsValidState(json));
        var back = GameStates.Read<FindMeState>(json)!;
        Assert.Equal(state.Shown, back.Shown);
        Assert.Equal(state.Unseen, back.Unseen);
        Assert.Equal(state.Words, back.Words);
        Assert.Equal((3, 4, 1), (back.Target, back.Found, back.Lang));
    }

    [Fact]
    public void The_other_states_come_back_the_same()
    {
        var listen = GameStates.Read<ListenState>(GameStates.Write(new ListenState { Lang = 3, Words = [1, 2], Steps = [1, 2, 1], Step = 2 }))!;
        Assert.Equal(new[] { 1, 2, 1 }, listen.Steps);
        Assert.Equal((2, 3), (listen.Step, listen.Lang));

        var phrase = GameStates.Read<PhraseState>(GameStates.Write(new PhraseState { Lang = 1, Rounds = [5, 6, 7], Step = 1 }))!;
        Assert.Equal(new[] { 5, 6, 7 }, phrase.Rounds);
        Assert.Equal(1, phrase.Step);

        var sequence = GameStates.Read<SequenceState>(GameStates.Write(new SequenceState { Lang = 1, Rounds = [[1, 2, 3], [4, 5]], Round = 1 }))!;
        Assert.Equal(2, sequence.Rounds.Count);
        Assert.Equal(new[] { 4, 5 }, sequence.Rounds[1]);
        Assert.Equal(1, sequence.Round);

        var steps = GameStates.Read<StepsState>(GameStates.Write(new StepsState { Lang = 1, Words = [9, 8, 7, 6], Question = 2, FiftyFifty = false, Audience = true }))!;
        Assert.Equal((2, false, true), (steps.Question, steps.FiftyFifty, steps.Audience));
    }

    [Theory]
    [InlineData("{\"v\":2,\"lang\":1}")]       // a version this code does not know
    [InlineData("{\"lang\":1}")]               // no version
    [InlineData("not json")]
    [InlineData("")]
    [InlineData(null)]
    public void An_unknown_or_broken_state_is_not_read(string? json)
        => Assert.Null(GameStates.Read<ListenState>(json));
}
