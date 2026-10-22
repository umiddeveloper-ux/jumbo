using Jumbo.Api.Auth;
using Jumbo.Api.Data;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

/// <summary>
/// The ads (see <see cref="Ad"/>). The site and the apps ask here "is there an ad for me now?" when a page is opened and every few minutes;
/// the answer is an ad only for a registered learner of the free version (Bronze), and the server notes that he has seen it, so one ad is
/// not shown twice at once on the site and in the app.
/// </summary>
[ApiController]
[Route("api/ads")]
[RequireAccessToken]
public class AdsController(AppDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Between two ads (any ads) a learner has a rest of this many minutes.</summary>
    public const int MinGapMinutes = 10;

    [HttpGet("next")]
    public async Task<ActionResult<AdForUserDto>> Next()
    {
        Response.Headers.CacheControl = "no-store";
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var user = await db.Users.AsNoTracking().Where(u => u.UserId == userId)
            .Select(u => new { u.Role, u.Plan, u.PlanUntil, u.AppLanguageId }).SingleAsync();
        var now = DateTime.UtcNow;

        // only learners of the free version
        if (user.Role != UserRole.Learner || Plans.Effective(user.Plan, user.PlanUntil, now) != Plans.Bronze) return NoContent();

        var views = await db.AdViews.Where(v => v.UserId == userId).ToListAsync();
        if (views.Count > 0 && (now - views.Max(v => v.LastShownAt)).TotalMinutes < MinGapMinutes) return NoContent();

        var lang = TopicVideoPaths.LanguageCode(user.AppLanguageId);
        var candidates = await db.Ads.AsNoTracking()
            .Where(a => a.IsActive && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now) && (a.Lang == null || a.Lang == lang))
            .OrderBy(a => a.StartsAt).ToListAsync();

        var due = candidates.FirstOrDefault(a =>
        {
            var view = views.FirstOrDefault(v => v.AdId == a.AdId);
            if (a.Mode == AdModes.Daily)
            {
                // once a day (the day of the app is UTC+5), from the time of the day that is set
                var local = now.AddHours(5);
                return local.TimeOfDay.TotalMinutes >= a.DailyAtMinutes && (view is null || view.LastShownAt.AddHours(5).Date < local.Date);
            }
            if (view is null) return true;                                                              // never seen
            return a.Mode == AdModes.Periodic && (now - view.LastShownAt).TotalMinutes >= Math.Max(1, a.IntervalMinutes);
        });
        if (due is null) return NoContent();

        var seen = views.FirstOrDefault(v => v.AdId == due.AdId);
        if (seen is null) db.AdViews.Add(new AdView { UserId = userId, AdId = due.AdId, LastShownAt = now, Count = 1 });
        else { seen.LastShownAt = now; seen.Count++; }
        await db.SaveChangesAsync();

        var full = await db.Ads.AsNoTracking().SingleAsync(a => a.AdId == due.AdId);
        return new AdForUserDto { Id = full.AdId, Title = full.Title, Html = full.Html, ImageDataUrl = AdImages.ToDataUrl(full) };
    }
}

/// <summary>The pictures of the ads: checking an uploaded one and turning one into a data: url.</summary>
public static class AdImages
{
    public const int MaxBytes = 1024 * 1024;

    public static string? ToDataUrl(Ad ad) =>
        ad.ImageData is { Length: > 0 } && ad.ImageType is not null ? $"data:{ad.ImageType};base64,{Convert.ToBase64String(ad.ImageData)}" : null;

    /// <summary>Reads a data: url; returns the bytes and the type, or null when it is not a good picture (what it says is not trusted: the first bytes are checked).</summary>
    public static (byte[] Data, string Type)? Parse(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl) || !dataUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return null;
        int comma = dataUrl.IndexOf(',');
        if (comma < 0 || !dataUrl[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase)) return null;
        byte[] data;
        try { data = Convert.FromBase64String(dataUrl[(comma + 1)..]); } catch { return null; }
        if (data.Length is 0 or > MaxBytes) return null;

        var type = Sniff(data);
        return type is null ? null : (data, type);
    }

    private static string? Sniff(byte[] d)
    {
        if (d.Length >= 8 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47) return "image/png";
        if (d.Length >= 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF) return "image/jpeg";
        if (d.Length >= 6 && d[0] == 0x47 && d[1] == 0x49 && d[2] == 0x46 && d[3] == 0x38) return "image/gif";
        if (d.Length >= 12 && d[0] == 'R' && d[1] == 'I' && d[2] == 'F' && d[3] == 'F' && d[8] == 'W' && d[9] == 'E' && d[10] == 'B' && d[11] == 'P') return "image/webp";
        return null;
    }
}
