using Jumbo.Api.Data;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Services;

// Commands for the person who owns the server (nobody else can run them: they need a shell inside the API container):
//   dotnet Jumbo.Api.dll --list-admins                      the logins that have the Admin role
//   NEW_PASSWORD=... dotnet Jumbo.Api.dll --reset-password <login>
// The new password comes from the NEW_PASSWORD environment variable so that it never appears in a command line or a log.
public static class AdminCli
{
    private const int MinPasswordLength = 6;

    public static bool IsCommand(string[] args) => args.Contains("--reset-password") || args.Contains("--list-admins");

    public static async Task<int> RunAsync(WebApplication app, string[] args)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (args.Contains("--list-admins"))
        {
            var admins = await db.Users.AsNoTracking().Where(u => u.Role == UserRole.Admin)
                .Select(u => new { u.Username, u.FirstName, u.LastName }).ToListAsync();
            Console.WriteLine(admins.Count == 0
                ? "No user has the Admin role yet (set ADMIN_USERNAMES in the server's .env to a registered login, then restart the api)."
                : "Admin logins:");
            foreach (var a in admins) Console.WriteLine($"  {a.Username}  ({a.FirstName} {a.LastName})".TrimEnd());
            return 0;
        }

        var index = Array.IndexOf(args, "--reset-password");
        var login = index >= 0 && index + 1 < args.Length ? args[index + 1].Trim() : "";
        var password = Environment.GetEnvironmentVariable("NEW_PASSWORD") ?? "";
        if (login.Length == 0 || login.StartsWith("--"))
        {
            Console.Error.WriteLine("Usage: NEW_PASSWORD=<new password> dotnet Jumbo.Api.dll --reset-password <login>");
            return 1;
        }
        if (password.Length < MinPasswordLength)
        {
            Console.Error.WriteLine($"The new password must have at least {MinPasswordLength} characters (NEW_PASSWORD is empty or too short).");
            return 1;
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Username != null && u.Username.ToLower() == login.ToLower());
        if (user is null)
        {
            Console.Error.WriteLine($"No user with the login \"{login}\". Use --list-admins to see the admin logins.");
            return 2;
        }

        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        await db.SaveChangesAsync();
        Console.WriteLine($"The password of \"{user.Username}\" (role {user.Role}) was changed. You can log in with the new password now.");
        return 0;
    }
}
