using Jumbo.Api.Auth;
using Jumbo.Api.Data;
using Jumbo.Api.Services.Telegram;
using Jumbo.Api.Services;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Jumbo.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController(AppDbContext db, ICurrentUserService currentUser, DailyProgressService daily, TelegramNotifier telegramNotifier, LoginThrottle throttle) : ControllerBase
{
    // "Too many wrong passwords": the account is locked for a while (see LoginThrottle). The clients show their own text with the minutes.
    private ObjectResult TooMany(TimeSpan left)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(left.TotalSeconds));
        Response.Headers.RetryAfter = seconds.ToString();
        return StatusCode(429, new { error = "TOO_MANY", retryAfterSeconds = seconds });
    }

    // The same for the places where a signed-in learner proves the password (a new login or password, deleting the account).
    private ObjectResult TooManyPasswordTries(TimeSpan left) =>
        StatusCode(429, new { error = $"Слишком много неверных паролей. Попробуйте через {LoginThrottle.Minutes(left)} мин." });

    private static readonly PasswordHasher<User> PasswordHasher = new();

    // Загруженные аватары кодируются как data:-URL прямо в JSON; ограничиваем размер, чтобы не раздувать базу.
    // Клиент ограничивает исходный файл 1 МБ; base64 добавляет ~33%, так что берём запас.
    private const int MaxAvatarLength = 1_500_000;

    [HttpPost("register")]
    public async Task<ActionResult<RegisterUserResponse>> Register(RegisterUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Логин и пароль обязательны." });

        if (request.AvatarSvg?.Length > MaxAvatarLength)
            return BadRequest(new { error = "Изображение аватара слишком большое." });

        var usernameTaken = await db.Users.AsNoTracking()
            .AnyAsync(u => u.Username != null && u.Username.ToLower() == request.Username.ToLower());
        if (usernameTaken)
            return Conflict(new { error = "Этот логин уже занят." });

        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            AppLanguageId = request.AppLanguageId,
            StudyLanguageId = request.StudyLanguageId,
            TranslationLanguageId = request.TranslationLanguageId,
            TranslationLanguageId2 = request.TranslationLanguageId2,
            Username = request.Username,
            AvatarSvg = request.AvatarSvg,
            Phone = Jumbo.Shared.PhoneNumbers.Clean(request.Phone),
            Region = Clean(request.Region, 40),
            StaffCandidate = request.RegisterAsTeacher,
            AccessToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        };
        user.PasswordHash = PasswordHasher.HashPassword(user, request.Password);

        // School and team are picked from the lists, never typed. A team the teacher has not confirmed yet stays
        // pending. An unknown / archived team is ignored: registration must not fail over an optional field.
        int? pendingTeamId = null;
        if (request.ClassId is { } teamId)
        {
            var team = await db.SchoolClasses.AsNoTracking().SingleOrDefaultAsync(c => c.ClassId == teamId && !c.IsArchived);
            if (team is not null)
            {
                await ClassesController.ApplyTeamAsync(db, user, team);
                pendingTeamId = team.ClassId;
            }
        }

        db.Users.Add(user);
        await db.SaveChangesAsync();
        if (pendingTeamId is { } pending) await telegramNotifier.NotifyPendingAsync(pending, user.UserId);

        return new RegisterUserResponse { UserId = user.UserId, AccessToken = user.AccessToken };
    }

    // Lets the registration wizard report a taken login on the step where it is typed.
    [HttpGet("username-available")]
    public async Task<ActionResult<bool>> UsernameAvailable([FromQuery] string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;
        var name = username.Trim().ToLower();
        return !await db.Users.AsNoTracking().AnyAsync(u => u.Username != null && u.Username.ToLower() == name);
    }

    private static string? Clean(string? value, int max)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        return v.Length > max ? v[..max] : v;
    }

    [HttpPost("login")]
    public async Task<ActionResult<RegisterUserResponse>> Login(LoginRequest request)
    {
        var key = LoginThrottle.ForLogin(request.Username);
        if (throttle.Blocked(key) is { } blocked) return TooMany(blocked);          // locked: not even the right password is tried

        var user = await db.Users.SingleOrDefaultAsync(u => u.Username != null && u.Username.ToLower() == request.Username.ToLower());
        var ok = user?.PasswordHash is not null &&
                 PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password ?? string.Empty) != PasswordVerificationResult.Failed;
        if (!ok)
        {
            // the fifth wrong password in a row starts the lock and is answered with it
            if (throttle.Fail(key) is { } lockedFor) return TooMany(lockedFor);
            return Unauthorized(new { error = "Неверный логин или пароль." });
        }

        throttle.Success(key);
        return new RegisterUserResponse { UserId = user.UserId, AccessToken = user.AccessToken };
    }

    private const int MinPasswordLength = 6;

    // Deletes the signed-in learner's account and everything that belongs to it (progress, words, awards, purchases, daily records, exam
    // results, Telegram link). Google Play requires an in-app way to do this. Teachers, directors and the admin are not deleted here:
    // their classes and exams belong to the school, so the admin does it.
    [HttpPost("me/delete")]
    [RequireAccessToken]
    public async Task<IActionResult> DeleteAccount(DeleteAccountRequest request)
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var user = await db.Users.SingleAsync(u => u.UserId == userId);

        if (user.Role != UserRole.Learner)
            return Conflict(new { error = "Аккаунт учителя, директора или администратора удаляет администратор. Напишите нам." });

        if (user.PasswordHash is not null)
        {
            var key = LoginThrottle.ForUser(userId);
            if (throttle.Blocked(key) is { } blocked) return TooManyPasswordTries(blocked);
            var verified = !string.IsNullOrEmpty(request.CurrentPassword) &&
                PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) != PasswordVerificationResult.Failed;
            if (!verified)
            {
                if (throttle.Fail(key) is { } lockedFor) return TooManyPasswordTries(lockedFor);
                return BadRequest(new { error = "Неверный текущий пароль." });
            }
            throttle.Success(key);
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        // rows without a cascade from the user, then the user (the rest of its data goes with it through the foreign keys)
        await db.TelegramConnections.Where(t => t.UserId == userId).ExecuteDeleteAsync();
        await db.QrLoginSessions.Where(q => q.UserId == userId).ExecuteDeleteAsync();
        await db.Users.Where(u => u.UserId == userId).ExecuteDeleteAsync();
        await tx.CommitAsync();
        return NoContent();
    }

    [HttpPut("me/credentials")]
    [RequireAccessToken]
    public async Task<IActionResult> ChangeCredentials(ChangeCredentialsRequest request)
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var user = await db.Users.SingleAsync(u => u.UserId == userId);

        if (user.PasswordHash is not null)
        {
            var key = LoginThrottle.ForUser(userId);
            if (throttle.Blocked(key) is { } blocked) return TooManyPasswordTries(blocked);
            var verified = !string.IsNullOrEmpty(request.CurrentPassword) &&
                PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) != PasswordVerificationResult.Failed;
            if (!verified)
            {
                if (throttle.Fail(key) is { } lockedFor) return TooManyPasswordTries(lockedFor);
                return BadRequest(new { error = "Неверный текущий пароль." });
            }
            throttle.Success(key);
        }

        var newUsername = request.NewUsername?.Trim();
        if (!string.IsNullOrEmpty(newUsername) && !string.Equals(newUsername, user.Username, StringComparison.Ordinal))
        {
            var taken = await db.Users.AsNoTracking()
                .AnyAsync(u => u.UserId != userId && u.Username != null && u.Username.ToLower() == newUsername.ToLower());
            if (taken)
                return Conflict(new { error = "Этот логин уже занят." });

            user.Username = newUsername;
        }

        if (!string.IsNullOrEmpty(request.NewPassword))
        {
            if (request.NewPassword.Length < MinPasswordLength)
                return BadRequest(new { error = $"Пароль должен содержать не менее {MinPasswordLength} символов." });

            user.PasswordHash = PasswordHasher.HashPassword(user, request.NewPassword);
        }

        // Legacy accounts may have neither; a password without a login would be unusable.
        if (string.IsNullOrEmpty(user.Username))
            return BadRequest(new { error = "Логин обязателен." });

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("me/daily-goal")]
    [RequireAccessToken]
    public async Task<ActionResult<DailyProgressDto>> SetDailyGoal(SetDailyGoalRequest request)
    {
        if (!DailyProgressService.AllowedGoals.Contains(request.GoalWords))
            return BadRequest(new { error = "Недопустимая цель дня." });

        var userId = (await currentUser.GetUserIdAsync())!.Value;
        return await daily.SetGoalAsync(userId, request.GoalWords);
    }

    [HttpGet("me")]
    [RequireAccessToken]
    public async Task<ActionResult<User>> GetMe()
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == userId);
        await ExpirePlanAsync(user);
        if (user.Role == UserRole.Learner)
            user.PendingStaffRole = await db.StaffRequests.AsNoTracking()
                .Where(r => r.UserId == userId && r.Status == StaffRequestStatus.Pending)
                .Select(r => (UserRole?)r.RequestedRole).FirstOrDefaultAsync();

        // the pictures of the worn pet and badge
        var worn = new[] { user.PetKey, user.BadgeKey }.Where(k => !string.IsNullOrEmpty(k)).ToList();
        if (worn.Count > 0)
        {
            var pics = await (from m in db.Markets.AsNoTracking()
                              where m.LanguageId == 1 && worn.Contains(m.Key)
                              join sv in db.SvgImages.AsNoTracking() on m.SvgImageId equals sv.SvgImageId
                              select new { m.Key, sv.SVG }).ToDictionaryAsync(x => x.Key, x => x.SVG);
            user.PetSvg = user.PetKey is { } pk ? pics.GetValueOrDefault(pk) : null;
            user.BadgeSvg = user.BadgeKey is { } bk ? pics.GetValueOrDefault(bk) : null;
        }
        return user;
    }

    /// <summary>A paid version whose month is over becomes bronze again (the row is changed once, the next read finds bronze).</summary>
    private async Task ExpirePlanAsync(User user)
    {
        var effective = Jumbo.Shared.Plans.Effective(user.Plan, user.PlanUntil, DateTime.UtcNow);
        if (effective == user.Plan) return;
        user.Plan = effective;
        if (effective == Jumbo.Shared.Plans.Bronze) user.PlanUntil = null;
        await db.Users.Where(u => u.UserId == user.UserId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Plan, effective).SetProperty(u => u.PlanUntil, (DateTime?)null));
    }

    /// <summary>The learner picks his own avatar. While a bought avatar is worn it is only remembered (it comes back when the bought one is taken off).</summary>
    [HttpPut("me/avatar")]
    [RequireAccessToken]
    public async Task<ActionResult<User>> SetAvatar(SetAvatarRequest request)
    {
        var svg = request.AvatarSvg?.Trim();
        if (string.IsNullOrEmpty(svg) || !(svg.StartsWith("<svg") || svg.StartsWith("data:image/")))
            return BadRequest(new { error = "Неверный аватар." });
        if (svg.Length > MaxAvatarLength)
            return BadRequest(new { error = "Изображение аватара слишком большое." });

        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var user = await db.Users.SingleAsync(u => u.UserId == userId);
        if (user.AvatarItemKey is null) user.AvatarSvg = svg;
        else user.AvatarDefaultSvg = svg;
        await db.SaveChangesAsync();
        await ExpirePlanAsync(user);
        return user;
    }

    [HttpPut("me")]
    [RequireAccessToken]
    public async Task<ActionResult<User>> UpdateMe(UpdateUserRequest request)
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var user = await db.Users.SingleAsync(u => u.UserId == userId);

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.PhoneNumber = request.PhoneNumber;
        user.AppLanguageId = request.AppLanguageId;
        user.StudyLanguageId = request.StudyLanguageId;
        user.TranslationLanguageId = request.TranslationLanguageId;
        user.TranslationLanguageId2 = request.TranslationLanguageId2;
        user.LifeCount = request.LifeCount;
        user.CurrentBookId = request.CurrentBookId;
        user.LifeExpirationDate = request.LifeExpirationDate;
        user.LpPoints = request.LpPoints;
        user.CurrentLanguageLevel = request.CurrentLanguageLevel;
        user.HideTooltips = request.HideTooltips;
        user.AppTheme = request.AppTheme;
        user.Phone = Jumbo.Shared.PhoneNumbers.Clean(request.Phone);
        // School and team are never taken from this request: they come from the pick lists (ClassesController).
        // The region follows the school, so it is only editable while the learner has no team.
        if (user.ClassId is null)
            user.Region = Clean(request.Region, 40);
        user.HideName = request.HideName;
        // A shield can be used up here, never bought (the market does that): a stale client must not bring back a spent one or lose a new one.
        if (request.ShieldCount is { } shields) user.ShieldCount = Math.Clamp(shields, 0, user.ShieldCount);

        await db.SaveChangesAsync();
        await ExpirePlanAsync(user);
        return user;
    }
}
