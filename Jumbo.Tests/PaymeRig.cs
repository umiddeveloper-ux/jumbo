using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Jumbo.Api.Data;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jumbo.Tests;

/// <summary>A clock the tests move by hand (the 12 hour timeout of a Payme transaction).</summary>
public class TestClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}

// Lets a test say which address the call comes from (TestServer has none): the header X-Test-Remote-IP becomes the connection's address.
internal class TestIpStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            if (context.Request.Headers.TryGetValue("X-Test-Remote-IP", out var ip) && IPAddress.TryParse(ip.ToString(), out var address))
                context.Connection.RemoteIpAddress = address;
            await nextMiddleware();
        });
        next(app);
    };
}

/// <summary>A running API for the Payme tests with a clock of its own and settings changed where a test needs it, plus the calls the tests make.</summary>
public sealed class PaymeRig : IDisposable
{
    public const string Login = "Paycom";
    public const string Key = "test-key-of-the-tests";
    public const string MerchantId = "test-merchant-id";

    public TestClock Clock { get; } = new(DateTimeOffset.UtcNow);
    public WebApplicationFactory<Program> Factory { get; }

    public PaymeRig(WebApplicationFactory<Program> baseFactory, Dictionary<string, string?>? settings = null, bool testIp = false)
    {
        Factory = baseFactory.WithWebHostBuilder(b =>
        {
            if (settings is not null) b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(settings));
            b.ConfigureTestServices(s =>
            {
                s.RemoveAll<TimeProvider>();
                s.AddSingleton<TimeProvider>(Clock);
                if (testIp) s.AddTransient<IStartupFilter, TestIpStartupFilter>();
            });
        });
        SeedPrices();
    }

    public void Dispose() => Factory.Dispose();

    // ---- data ---------------------------------------------------------------------------------------------------------------------------

    public T WithDb<T>(Func<AppDbContext, T> run)
    {
        using var scope = Factory.Services.CreateScope();
        return run(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>
    /// The four prices (so'm). Three are set to the known value every time; gold / year, the price that two tests change, is only filled in when it is
    /// missing (a rig that starts while one of them runs must not undo its change) unless restoreGoldYear says so.
    /// </summary>
    public void SeedPrices(bool restoreGoldYear = false) => WithDb(db =>
    {
        // an upsert: two test classes start at the same time and both seed
        void Set(string plan, string period, long amount, bool overwrite) => db.Database.ExecuteSqlRaw(
            "INSERT INTO plan_prices (plan, period, amount, updated_at) VALUES ({0}, {1}, {2}, now()) ON CONFLICT (plan, period) DO "
            + (overwrite ? "UPDATE SET amount = EXCLUDED.amount, updated_at = now()" : "NOTHING"), plan, period, amount);
        Set(Plans.Silver, Plans.Month, 50_000, true);
        Set(Plans.Silver, Plans.Year, 500_000, true);
        Set(Plans.Gold, Plans.Month, 90_000, true);
        Set(Plans.Gold, Plans.Year, 900_000, restoreGoldYear);
        return 0;
    });

    public async Task<(HttpClient client, int userId, string login, string password)> NewUserAsync(UserRole role = UserRole.Learner)
    {
        var login = "t" + Guid.NewGuid().ToString("N")[..12];
        const string password = "secret-123";
        var anon = Factory.CreateClient();
        var response = await anon.PostAsJsonAsync("/api/users/register", new RegisterUserRequest
        {
            FirstName = "Test", LastName = "User", AppLanguageId = 1, StudyLanguageId = 1, TranslationLanguageId = 3, Username = login, Password = password,
        });
        response.EnsureSuccessStatusCode();
        var registered = (await response.Content.ReadFromJsonAsync<RegisterUserResponse>())!;
        if (role != UserRole.Learner)
            WithDb(db => { db.Users.Find(registered.UserId)!.Role = role; db.SaveChanges(); return 0; });
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Access-Token", registered.AccessToken);
        return (client, registered.UserId, login, password);
    }

    /// <summary>An order of the learner through the site's API; returns its number and the amount in so'm.</summary>
    public async Task<(long orderId, long amountSum, string checkoutUrl)> NewOrderAsync(HttpClient learner, string plan = Plans.Silver, string period = Plans.Month, string lang = "en")
    {
        var response = await learner.PostAsJsonAsync("/api/payments/orders", new CreatePaymentOrderRequest { Plan = plan, Period = period, Lang = lang });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var o = (await response.Content.ReadFromJsonAsync<CreatePaymentOrderResponse>())!;
        return (o.OrderId, o.AmountSum, o.CheckoutUrl);
    }

    public (string? plan, DateTime? until) PlanOf(int userId) => WithDb(db =>
    {
        var u = db.Users.AsNoTracking().Single(x => x.UserId == userId);
        return ((string?)u.Plan, u.PlanUntil);
    });

    public string OrderStatus(long orderId) => WithDb(db => db.PaymentOrders.AsNoTracking().Single(o => o.PaymentOrderId == orderId).Status);

    public static string NewPaymeId() => Guid.NewGuid().ToString("N")[..24];

    // ---- the calls of Payme -------------------------------------------------------------------------------------------------------------

    public static string Basic(string login = Login, string key = Key) => "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{login}:{key}"));

    /// <summary>One call of the Merchant API. auth: null = no header; otherwise the whole value of the Authorization header (default: the right one).</summary>
    public async Task<PaymeAnswer> CallAsync(string method, object? @params, int id = 1, string? auth = "right", HttpClient? client = null, string? remoteIp = null,
                                              string? forwardedFor = null, HttpMethod? verb = null, string? rawBody = null)
    {
        client ??= Factory.CreateClient();
        var request = new HttpRequestMessage(verb ?? HttpMethod.Post, "/api/payme")
        {
            Content = new StringContent(rawBody ?? JsonSerializer.Serialize(new { method, @params, id }), Encoding.UTF8, "application/json")
        };
        if (auth is not null) request.Headers.TryAddWithoutValidation("Authorization", auth == "right" ? Basic() : auth);
        if (remoteIp is not null) request.Headers.Add("X-Test-Remote-IP", remoteIp);
        if (forwardedFor is not null) request.Headers.Add("X-Forwarded-For", forwardedFor);
        var response = await client.SendAsync(request);
        return new PaymeAnswer(response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    public Task<PaymeAnswer> CheckPerformAsync(long orderId, long amountTiyin) => CallAsync("CheckPerformTransaction", new { amount = amountTiyin, account = new { order_id = orderId.ToString() } });

    public Task<PaymeAnswer> CreateAsync(string paymeId, long orderId, long amountTiyin, long? time = null) =>
        CallAsync("CreateTransaction", new { id = paymeId, time = time ?? Clock.GetUtcNow().ToUnixTimeMilliseconds(), amount = amountTiyin, account = new { order_id = orderId.ToString() } });

    public Task<PaymeAnswer> PerformAsync(string paymeId) => CallAsync("PerformTransaction", new { id = paymeId });
    public Task<PaymeAnswer> CancelAsync(string paymeId, int reason = 5) => CallAsync("CancelTransaction", new { id = paymeId, reason });
    public Task<PaymeAnswer> CheckAsync(string paymeId) => CallAsync("CheckTransaction", new { id = paymeId });
}

/// <summary>The answer of the Merchant API: the HTTP status and the body, with the parts a test looks at.</summary>
public class PaymeAnswer(HttpStatusCode status, string body)
{
    public HttpStatusCode Status { get; } = status;
    public string Body { get; } = body;
    public JsonElement Root { get; } = JsonDocument.Parse(body).RootElement.Clone();

    public bool IsError => Root.TryGetProperty("error", out _);
    public int Code => Root.GetProperty("error").GetProperty("code").GetInt32();
    public JsonElement Error => Root.GetProperty("error");
    public JsonElement Result => Root.GetProperty("result");
    public string ResultText => Result.GetRawText();

    public int State => Result.GetProperty("state").GetInt32();
}
