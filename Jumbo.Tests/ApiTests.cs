using System.Net;
using System.Net.Http.Json;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;

namespace Jumbo.Tests;

// The rules that the clients rely on, checked against the real API and database. These are the places that broke before.
public class ApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static LoginRequest Login(string user, string password) => new() { Username = user, Password = password };

    [Fact]
    public async Task Register_then_login_gives_the_same_token_and_a_taken_login_is_refused()
    {
        var (_, userId, login, password) = await api.NewUserAsync();

        var anon = api.CreateClient();
        var ok = await anon.PostAsJsonAsync("/api/users/login", Login(login.ToUpperInvariant(), password));   // the case of the login does not matter
        ok.EnsureSuccessStatusCode();
        Assert.Equal(userId, (await ok.Content.ReadFromJsonAsync<RegisterUserResponse>())!.UserId);

        var again = await anon.PostAsJsonAsync("/api/users/register", new RegisterUserRequest
        {
            FirstName = "A", LastName = "B", AppLanguageId = 1, StudyLanguageId = 1, TranslationLanguageId = 3, Username = login, Password = password,
        });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        Assert.False(await anon.GetFromJsonAsync<bool>($"/api/users/username-available?username={login}"));
        Assert.True(await anon.GetFromJsonAsync<bool>("/api/users/username-available?username=nobody-" + Guid.NewGuid().ToString("N")));
    }

    [Fact]
    public async Task A_wrong_password_is_refused_and_five_in_a_row_lock_the_account()
    {
        var (_, _, login, password) = await api.NewUserAsync();
        var anon = api.CreateClient();

        for (var i = 0; i < 4; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/users/login", Login(login, "wrong"))).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await anon.PostAsJsonAsync("/api/users/login", Login(login, "wrong"))).StatusCode);
        // locked: even the right password is not tried
        Assert.Equal(HttpStatusCode.TooManyRequests, (await anon.PostAsJsonAsync("/api/users/login", Login(login, password))).StatusCode);
    }

    [Fact]
    public async Task Me_needs_a_token()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().GetAsync("/api/users/me")).StatusCode);
        var (client, _, _, _) = await api.NewUserAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/me")).StatusCode);
    }

    [Theory]
    [InlineData(UserRole.Learner, HttpStatusCode.Forbidden)]
    [InlineData(UserRole.Teacher, HttpStatusCode.Forbidden)]
    [InlineData(UserRole.Director, HttpStatusCode.Forbidden)]
    [InlineData(UserRole.Admin, HttpStatusCode.OK)]
    public async Task The_level_test_editor_is_for_the_admin_only(UserRole role, HttpStatusCode expected)
    {
        var (client, _, _, _) = await api.NewUserAsync(role);
        Assert.Equal(expected, (await client.GetAsync("/api/admin/placement/questions")).StatusCode);
    }

    [Fact]
    public async Task The_level_test_editor_without_a_token_is_401()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().GetAsync("/api/admin/placement/questions")).StatusCode);

    [Theory]
    [InlineData("A1")]
    [InlineData("A2")]
    [InlineData("B1")]
    [InlineData("B2")]
    [InlineData("b2")]
    public async Task A_level_test_block_is_open_to_visitors_and_every_answer_points_to_an_option(string level)
    {
        var block = await api.CreateClient().GetFromJsonAsync<PlacementBlockDto>($"/api/placement/block/{level}");
        Assert.NotNull(block);
        Assert.Equal(level.ToUpperInvariant(), block.Level);
        Assert.NotEmpty(block.Questions);
        Assert.All(block.Questions, q =>
        {
            Assert.InRange(q.Answer, 0, q.Options.Count - 1);
            Assert.Equal(q.Options.Count, q.Options.Distinct().Count());       // no option twice
            Assert.Contains(q.Skill, PlacementSkill.All);
        });
        Assert.Equal(block.Questions.Count, block.Questions.Select(q => q.Id).Distinct().Count());
        // the reading questions carry their text, the listening ones what is said
        Assert.All(block.Questions.Where(q => q.Skill == PlacementSkill.Reading), q => Assert.False(string.IsNullOrWhiteSpace(q.Passage)));
        Assert.All(block.Questions.Where(q => q.Skill == PlacementSkill.Listening), q => Assert.NotEmpty(q.Audio));
    }

    [Fact]
    public async Task An_unknown_level_is_404()
        => Assert.Equal(HttpStatusCode.NotFound, (await api.CreateClient().GetAsync("/api/placement/block/C2")).StatusCode);

    [Fact]
    public async Task A_missing_recording_is_404()
        => Assert.Equal(HttpStatusCode.NotFound, (await api.CreateClient().GetAsync("/api/placement/audio/2000000000")).StatusCode);

    [Fact]
    public async Task The_admin_can_upload_a_recording_and_the_visitor_can_play_it()
    {
        var (admin, _, _, _) = await api.NewUserAsync(UserRole.Admin);
        var questions = await admin.GetFromJsonAsync<List<PlacementAdminQuestionDto>>("/api/admin/placement/questions");
        var listening = questions!.First(q => q.Skill == PlacementSkill.Listening);

        // a tiny valid WAV header is enough: the server checks the bytes, not the name
        var wav = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x24, 0, 0, 0, 0x57, 0x41, 0x56, 0x45, 0x66, 0x6d, 0x74, 0x20, 16, 0, 0, 0, 1, 0, 1, 0, 0x44, 0xac, 0, 0, 0x88, 0x58, 1, 0, 2, 0, 16, 0, 0x64, 0x61, 0x74, 0x61, 0, 0, 0, 0 };
        var put = new ByteArrayContent(wav);
        put.Headers.ContentType = new("application/octet-stream");
        var upload = await admin.PutAsync($"/api/admin/placement/questions/{listening.Id}/audio", put);
        Assert.True(upload.IsSuccessStatusCode, await upload.Content.ReadAsStringAsync());

        var played = await api.CreateClient().GetAsync($"/api/placement/audio/{listening.Id}");
        Assert.Equal(HttpStatusCode.OK, played.StatusCode);
        Assert.Equal(wav, await played.Content.ReadAsByteArrayAsync());

        // not a sound: refused
        var bad = new ByteArrayContent("this is a text file"u8.ToArray());
        bad.Headers.ContentType = new("application/octet-stream");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync($"/api/admin/placement/questions/{listening.Id}/audio", bad)).StatusCode);

        Assert.True((await admin.DeleteAsync($"/api/admin/placement/questions/{listening.Id}/audio")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task A_learner_can_delete_the_account_and_the_token_stops_working()
    {
        var (client, _, login, password) = await api.NewUserAsync();
        // holding the token is not enough: the password must be given too
        var refused = await client.PostAsJsonAsync("/api/users/me/delete", new DeleteAccountRequest { CurrentPassword = "not-the-password" });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/me")).StatusCode);

        var response = await client.PostAsJsonAsync("/api/users/me/delete", new DeleteAccountRequest { CurrentPassword = password });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().PostAsJsonAsync("/api/users/login", Login(login, password))).StatusCode);
    }
}
