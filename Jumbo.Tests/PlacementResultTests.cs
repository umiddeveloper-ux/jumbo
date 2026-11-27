using System.Net;
using System.Net.Http.Json;
using Jumbo.Api.Data;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Jumbo.Tests;

// The level test (CEFR) result of a signed-in learner is stored, the stage is worked out on the server, and the teacher of the team sees it.
public class PlacementResultTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static SavePlacementResultRequest Shares(params double[] shares) => new() { Shares = shares.ToList() };

    private async Task<int> RowsAsync(int userId)
    {
        using var scope = api.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().PlacementAttempts.CountAsync(r => r.UserId == userId);
    }

    [Fact]
    public async Task A_learner_result_is_stored_with_the_stage_worked_out_by_the_server()
    {
        var (client, userId, _, _) = await api.NewUserAsync();

        var response = await client.PostAsJsonAsync("/api/placement/result", Shares(0.9, 0.5));
        response.EnsureSuccessStatusCode();
        var saved = (await response.Content.ReadFromJsonAsync<PlacementSavedDto>())!;

        Assert.Equal("a2", saved.Stage);                     // 90 % of A1, some of A2
        Assert.Equal("A2", saved.Level);
        Assert.Equal(4, saved.RecommendedBook);
        Assert.Equal(new double?[] { 0.9, 0.5, null, null }, saved.Shares);
        Assert.Equal(1, await RowsAsync(userId));
    }

    [Fact]
    public async Task The_client_cannot_send_a_stage_of_its_own()
    {
        var (client, _, _, _) = await api.NewUserAsync();

        // extra fields (a made-up stage) are ignored: only the shares count
        var junk = new StringContent("""{"shares":[0.2],"stage":"b2plus","level":"B2","recommendedBook":10}""", System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/placement/result", junk);
        response.EnsureSuccessStatusCode();
        var saved = (await response.Content.ReadFromJsonAsync<PlacementSavedDto>())!;
        Assert.Equal("starter", saved.Stage);
        Assert.Equal(1, saved.RecommendedBook);
    }

    [Theory]
    [InlineData(new double[] { })]                                   // nothing
    [InlineData(new[] { 1.5 })]                                      // not a share
    [InlineData(new[] { -0.1 })]
    [InlineData(new[] { 0.9, 0.9, 0.9, 0.9, 0.9 })]                  // five blocks do not exist
    [InlineData(new[] { 0.5, 0.9 })]                                 // the second block follows only after a good first one
    public async Task Shares_that_cannot_come_from_a_test_are_refused(double[] shares)
    {
        var (client, userId, _, _) = await api.NewUserAsync();
        var response = await client.PostAsJsonAsync("/api/placement/result", Shares(shares));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await RowsAsync(userId));
    }

    [Fact]
    public async Task Without_a_token_nothing_is_stored()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().PostAsJsonAsync("/api/placement/result", Shares(0.9))).StatusCode);

    [Theory]
    [InlineData(UserRole.Teacher)]
    [InlineData(UserRole.Director)]
    [InlineData(UserRole.Admin)]
    public async Task Teachers_and_admins_get_204_and_nothing_is_stored(UserRole role)
    {
        var (client, userId, _, _) = await api.NewUserAsync(role);
        var response = await client.PostAsJsonAsync("/api/placement/result", Shares(0.9, 0.9));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await RowsAsync(userId));
    }

    [Fact]
    public async Task My_gives_the_latest_attempt_and_how_many_there_are()
    {
        var (client, _, _, _) = await api.NewUserAsync();

        var none = (await client.GetFromJsonAsync<MyPlacementDto>("/api/placement/my"))!;
        Assert.Null(none.Latest);
        Assert.Equal(0, none.Count);

        (await client.PostAsJsonAsync("/api/placement/result", Shares(0.5))).EnsureSuccessStatusCode();
        await Task.Delay(20);
        (await client.PostAsJsonAsync("/api/placement/result", Shares(0.9, 0.9, 0.5))).EnsureSuccessStatusCode();

        var my = (await client.GetFromJsonAsync<MyPlacementDto>("/api/placement/my"))!;
        Assert.Equal(2, my.Count);
        Assert.Equal("b1", my.Latest!.Stage);
    }

    [Fact]
    public async Task At_most_twenty_attempts_a_day_are_stored()
    {
        var (client, userId, _, _) = await api.NewUserAsync();
        for (var i = 0; i < 20; i++)
            (await client.PostAsJsonAsync("/api/placement/result", Shares(0.5))).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/placement/result", Shares(0.5))).StatusCode);
        Assert.Equal(20, await RowsAsync(userId));
    }

    [Fact]
    public async Task Deleting_the_account_removes_the_results()
    {
        var (client, userId, _, password) = await api.NewUserAsync();
        (await client.PostAsJsonAsync("/api/placement/result", Shares(0.9, 0.5))).EnsureSuccessStatusCode();
        Assert.Equal(1, await RowsAsync(userId));

        var deleted = await client.PostAsJsonAsync("/api/users/me/delete", new DeleteAccountRequest { CurrentPassword = password });
        Assert.True(deleted.IsSuccessStatusCode, await deleted.Content.ReadAsStringAsync());
        Assert.Equal(0, await RowsAsync(userId));
    }

    // A team of a teacher with one approved pupil.
    private async Task<(HttpClient teacher, int classId, int pupilId, HttpClient pupil)> TeamAsync()
    {
        var (teacher, teacherId, _, _) = await api.NewUserAsync(UserRole.Teacher);
        var (pupil, pupilId, _, _) = await api.NewUserAsync();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var school = new School { Region = "Test", Name = "School " + Guid.NewGuid().ToString("N")[..6] };
        db.Schools.Add(school);
        await db.SaveChangesAsync();
        var cls = new SchoolClass { SchoolId = school.SchoolId, TeacherId = teacherId, Name = "Team " + Guid.NewGuid().ToString("N")[..6] };
        db.SchoolClasses.Add(cls);
        await db.SaveChangesAsync();
        (await db.Users.FindAsync(teacherId))!.SchoolId = school.SchoolId;
        var p = (await db.Users.FindAsync(pupilId))!;
        p.SchoolId = school.SchoolId; p.ClassId = cls.ClassId; p.ClassApproved = true;
        await db.SaveChangesAsync();
        return (teacher, cls.ClassId, pupilId, pupil);
    }

    [Fact]
    public async Task The_teacher_sees_the_latest_result_of_a_pupil_of_the_team()
    {
        var (teacher, classId, pupilId, pupil) = await TeamAsync();

        // before the test: no result
        var before = (await teacher.GetFromJsonAsync<ClassDetailDto>($"/api/teacher/classes/{classId}"))!;
        Assert.Null(before.Students.Single().Cefr);

        (await pupil.PostAsJsonAsync("/api/placement/result", Shares(0.9, 0.9, 0.5))).EnsureSuccessStatusCode();

        var list = (await teacher.GetFromJsonAsync<ClassDetailDto>($"/api/teacher/classes/{classId}"))!;
        Assert.Equal("b1", list.Students.Single().Cefr!.Stage);

        var one = (await teacher.GetFromJsonAsync<StudentProgressDto>($"/api/teacher/classes/{classId}/students/{pupilId}"))!;
        Assert.Equal("b1", one.Cefr!.Stage);
        Assert.Equal("B1", one.Cefr.Level);
    }

    [Fact]
    public async Task Another_teacher_does_not_see_the_pupil_or_the_result()
    {
        var (_, classId, pupilId, pupil) = await TeamAsync();
        (await pupil.PostAsJsonAsync("/api/placement/result", Shares(0.9))).EnsureSuccessStatusCode();
        var (stranger, _, _, _) = await api.NewUserAsync(UserRole.Teacher);

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/teacher/classes/{classId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/teacher/classes/{classId}/students/{pupilId}")).StatusCode);
    }
}
