using Jumbo.Api.Auth;
using Jumbo.Api.Data;
using Jumbo.Api.Services;
using Jumbo.Api.Services.Telegram;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("Default"))
    .UseSnakeCaseNamingConvention());

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<SeedDataService>();
builder.Services.AddScoped<DailyProgressService>();
builder.Services.AddScoped<Jumbo.Api.Services.LivesService>();      // lives and LP: counted here, not in the clients
builder.Services.AddScoped<RatingService>();
builder.Services.AddScoped<AwardService>();
builder.Services.AddScoped<ClassStatsService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<Jumbo.Api.Services.LoginThrottle>();      // protection against guessing passwords

// Telegram bot (token comes from the environment: Telegram__BotToken). Without a token the bot stays off.
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection("Telegram"));
builder.Services.AddHttpClient<TelegramClient>(c => { c.BaseAddress = new Uri("https://api.telegram.org/"); c.Timeout = TimeSpan.FromSeconds(10); })
    .RemoveAllLoggers();   // the request URL contains the bot token: it must never reach the logs
builder.Services.AddScoped<TelegramNotifier>();
builder.Services.AddScoped<WebLinkService>();
builder.Services.AddScoped<LearnerStatsService>();
builder.Services.AddScoped<TelegramPupilService>();
builder.Services.AddScoped<TelegramBotService>();
builder.Services.AddHostedService<TelegramBackgroundService>();

var app = builder.Build();

// Server-owner commands (list the admins, reset a password): run once and exit, the web server is not started.
if (Jumbo.Api.Services.AdminCli.IsCommand(args)) return await Jumbo.Api.Services.AdminCli.RunAsync(app, args);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Runs behind Nginx, which is the sole TLS terminator (now over plain http,
// https once a domain + cert are added) — the app itself must never redirect
// to https, since Kestrel here never listens on a TLS port.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var seed = scope.ServiceProvider.GetRequiredService<SeedDataService>();
    await seed.LoadSeedDataAsync();

    // New edition of some books: SeedData__ReloadBooks=1,2,3 replaces them with their SeedData files (learners' progress in
    // those books is deleted; a book that already matches its file is left alone).
    var reloadBooks = (app.Configuration["SeedData:ReloadBooks"] ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(b => int.TryParse(b, out var n) ? n : 0).Where(n => n > 0).ToList();
    if (reloadBooks.Count > 0) await seed.ReloadChangedBooksAsync(reloadBooks);

    // Books that are no longer part of the course: SeedData__RemoveBooks=11,12,13 deletes them from the database (with the
    // learners' progress in them). Their JSON files must be gone from SeedData, otherwise they are seeded again.
    var removeBooks = (app.Configuration["SeedData:RemoveBooks"] ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(b => int.TryParse(b, out var n) ? n : 0).Where(n => n > 0).ToList();
    if (removeBooks.Count > 0) await seed.RemoveBooksAsync(removeBooks);

    // Platform admins are configured on the server (Admin__Usernames=login1,login2). Listed logins get the Admin role
    // at startup; the role is never granted through the website.
    var adminLogins = (app.Configuration["Admin:Usernames"] ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(l => l.ToLowerInvariant()).ToList();
    if (adminLogins.Count > 0)
    {
        var admins = await db.Users.Where(u => u.Username != null && adminLogins.Contains(u.Username.ToLower()) && u.Role != Jumbo.Shared.Entities.UserRole.Admin).ToListAsync();
        foreach (var a in admins) a.Role = Jumbo.Shared.Entities.UserRole.Admin;
        if (admins.Count > 0) await db.SaveChangesAsync();
    }
}

app.Run();
return 0;
