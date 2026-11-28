using System.Net.Http.Json;
using Jumbo.Api.Data;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Jumbo.Tests;

// The whole API in memory, on a real PostgreSQL database (the model uses text[] and bytea, so no in-memory substitute is honest).
// The database comes from the environment variable JUMBO_TEST_DB; without it the local development server is used, with a database of
// its own called jumbo_tests (created by the migrations; it never touches the development data).
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string DefaultConnection = "Host=localhost;Port=5432;Database=jumbo_tests;Username=littlepolyglot;Password=littlepolyglot_dev";

    private readonly string _emptyContentRoot = Path.Combine(Path.GetTempPath(), "jumbo-tests-" + Guid.NewGuid().ToString("N"));

    public ApiFactory()
    {
        // read by Program.cs while the host is being built
        Environment.SetEnvironmentVariable("ConnectionStrings__Default",
            Environment.GetEnvironmentVariable("JUMBO_TEST_DB") ?? DefaultConnection);
        Environment.SetEnvironmentVariable("Telegram__BotToken", "");
        Environment.SetEnvironmentVariable("Admin__Usernames", "");
        // the Payme payment: throwaway values of the tests (no real cash register, no real codes)
        Environment.SetEnvironmentVariable("Payme__Login", PaymeRig.Login);
        Environment.SetEnvironmentVariable("Payme__Key", PaymeRig.Key);
        Environment.SetEnvironmentVariable("Payme__MerchantId", PaymeRig.MerchantId);
        Environment.SetEnvironmentVariable("Payme__Mxik", "11111111111111111");
        Environment.SetEnvironmentVariable("Payme__PackageCode", "1234567");
        Environment.SetEnvironmentVariable("Payme__VatPercent", "12");
        Environment.SetEnvironmentVariable("Payme__CheckIp", "false");
        Directory.CreateDirectory(_emptyContentRoot);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // an empty content root: the 140 MB of book files are not loaded for every test run (the level test questions are built into the API)
        builder.UseContentRoot(_emptyContentRoot);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { Directory.Delete(_emptyContentRoot, true); } catch { /* a temp folder */ }
    }

    // A new learner (a unique login) and the client that speaks as him.
    public async Task<(HttpClient client, int userId, string login, string password)> NewUserAsync(UserRole role = UserRole.Learner, int studyLanguageId = 1)
    {
        var login = "t" + Guid.NewGuid().ToString("N")[..12];
        const string password = "secret-123";
        var anon = CreateClient();
        var response = await anon.PostAsJsonAsync("/api/users/register", new RegisterUserRequest
        {
            FirstName = "Test", LastName = "User", AppLanguageId = 1, StudyLanguageId = studyLanguageId, TranslationLanguageId = 3,
            Username = login, Password = password,
        });
        response.EnsureSuccessStatusCode();
        var registered = (await response.Content.ReadFromJsonAsync<RegisterUserResponse>())!;

        if (role != UserRole.Learner)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FindAsync(registered.UserId);
            user!.Role = role;
            await db.SaveChangesAsync();
        }

        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Access-Token", registered.AccessToken);
        return (client, registered.UserId, login, password);
    }
}
