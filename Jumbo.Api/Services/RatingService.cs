using Jumbo.Api.Data;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Services;

// Leaderboard maths shared by the signed-in endpoint (api/rating) and the public top list (api/rating/top).
// Points = distinct words practised: the last 7 days ("week") or all words ever met ("all").
public class RatingService(AppDbContext db)
{
    public const int MaxTake = 100;

    // me == null means an anonymous visitor: only the "all" scope exists and there is no "Me" entry.
    public async Task<RatingDto> GetAsync(User? me, string scope, string period, int take)
    {
        take = Math.Clamp(take, 1, MaxTake);
        var result = new RatingDto { Scope = scope, Period = period };

        var players = db.Users.AsNoTracking().AsQueryable();
        switch (scope)
        {
            case "team":
                var team = Normalize(me?.Team);
                if (team is null) { result.Missing = true; return result; }
                players = players.Where(u => u.Team != null && u.Team.ToLower() == team);
                // "А_2020" can exist in several schools: a team is always school + name.
                var teamSchool = Normalize(me?.School);
                if (teamSchool is not null)
                    players = players.Where(u => u.School != null && u.School.ToLower() == teamSchool);
                break;
            case "school":
                var school = Normalize(me?.School);
                if (school is null) { result.Missing = true; return result; }
                players = players.Where(u => u.School != null && u.School.ToLower() == school);
                break;
            case "region":
                if (string.IsNullOrWhiteSpace(me?.Region)) { result.Missing = true; return result; }
                var region = me.Region;
                players = players.Where(u => u.Region == region);
                break;
        }

        var ids = await players.Select(u => u.UserId).ToListAsync();
        if (ids.Count == 0) return result;

        var points = await PointsAsync(ids, period);

        // Standard competition ranking: equal points share a place (1, 2, 2, 4).
        var ordered = points.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).ToList();
        result.Total = ordered.Count;

        var ranks = new Dictionary<int, int>();
        for (int i = 0; i < ordered.Count; i++)
            ranks[ordered[i].Key] = i > 0 && ordered[i].Value == ordered[i - 1].Value ? ranks[ordered[i - 1].Key] : i + 1;

        // Only these people's public fields are loaded (top N, plus the caller).
        var shown = ordered.Take(take).Select(kv => kv.Key).ToList();
        int? meId = me?.UserId;
        if (meId is { } mid && ranks.ContainsKey(mid) && !shown.Contains(mid)) shown.Add(mid);

        var info = await db.Users.AsNoTracking()
            .Where(u => shown.Contains(u.UserId))
            .Select(u => new
            {
                u.UserId, u.FirstName, u.LastName, u.Team, u.HideName, u.FrameKey, u.BadgeKey,
                // Preset avatars are small inline <svg>; uploaded photos (data: URLs) are never exposed.
                Avatar = u.AvatarSvg != null && u.AvatarSvg.StartsWith("<svg") ? u.AvatarSvg : null
            })
            .ToDictionaryAsync(u => u.UserId);

        // the pictures of the badges that are worn here
        var badgeKeys = info.Values.Select(u => u.BadgeKey).Where(k => !string.IsNullOrEmpty(k)).Distinct().ToList();
        var badges = badgeKeys.Count == 0
            ? new Dictionary<string, string>()
            : await (from mk in db.Markets.AsNoTracking()
                     where mk.LanguageId == 1 && badgeKeys.Contains(mk.Key)
                     join sv in db.SvgImages.AsNoTracking() on mk.SvgImageId equals sv.SvgImageId
                     select new { mk.Key, sv.SVG }).ToDictionaryAsync(x => x.Key, x => x.SVG);

        RatingEntryDto Entry(int userId)
        {
            var u = info[userId];
            return new RatingEntryDto
            {
                Rank = ranks[userId],
                Name = u.HideName ? null : DisplayName(u.FirstName, u.LastName),
                Team = string.IsNullOrWhiteSpace(u.Team) ? null : u.Team,
                AvatarSvg = u.HideName ? null : u.Avatar,
                FrameKey = u.HideName ? null : u.FrameKey,
                BadgeSvg = u.HideName || u.BadgeKey is null ? null : badges.GetValueOrDefault(u.BadgeKey),
                Words = points[userId],
                IsMe = userId == meId
            };
        }

        result.Entries = ordered.Take(take).Select(kv => Entry(kv.Key)).ToList();
        if (meId is { } m && ranks.ContainsKey(m)) result.Me = Entry(m);
        return result;
    }

    // Points of the given players: distinct words practised in the last 7 days ("week") or ever ("all").
    private async Task<Dictionary<int, int>> PointsAsync(List<int> ids, string period)
    {
        if (period == "week")
        {
            var from = DailyProgressService.Today.AddDays(-6);
            return await db.UserDailyProgresses.AsNoTracking()
                .Where(p => p.Date >= from && ids.Contains(p.UserId))
                .GroupBy(p => p.UserId)
                .Select(g => new { UserId = g.Key, Words = g.Sum(x => x.WordsCount) })
                .ToDictionaryAsync(x => x.UserId, x => x.Words);
        }
        return await db.UsersWords.AsNoTracking()
            .Where(w => ids.Contains(w.UserId))
            .GroupBy(w => w.UserId)
            .Select(g => new { UserId = g.Key, Words = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Words);
    }

    // A teacher's view of their own pupils (a team, or all their teams): the full name (the pupils' "hide my name" choice is
    // about the public leaderboards), their team, and everybody is listed, also those who have not practised yet.
    public async Task<RatingDto> GetForPupilsAsync(List<int> pupilIds, string period)
    {
        var result = new RatingDto { Scope = "team", Period = period };
        if (pupilIds.Count == 0) return result;

        var points = await PointsAsync(pupilIds, period);
        var pupils = await db.Users.AsNoTracking()
            .Where(u => pupilIds.Contains(u.UserId))
            .Select(u => new
            {
                u.UserId, u.FirstName, u.LastName, u.Team,
                Avatar = u.AvatarSvg != null && u.AvatarSvg.StartsWith("<svg") ? u.AvatarSvg : null
            })
            .ToListAsync();

        var ordered = pupils
            .Select(u => (User: u, Words: points.GetValueOrDefault(u.UserId)))
            .OrderByDescending(x => x.Words).ThenBy(x => x.User.LastName).ThenBy(x => x.User.FirstName)
            .ToList();
        result.Total = ordered.Count(x => x.Words > 0);

        var entries = new List<RatingEntryDto>();
        for (int i = 0; i < ordered.Count; i++)
        {
            var (u, words) = ordered[i];
            entries.Add(new RatingEntryDto
            {
                // equal points share a place; pupils without points have none (0, shown as a dash)
                Rank = words == 0 ? 0 : i > 0 && words == ordered[i - 1].Words ? entries[i - 1].Rank : i + 1,
                Name = $"{u.FirstName} {u.LastName}".Trim(),
                Team = string.IsNullOrWhiteSpace(u.Team) ? null : u.Team,
                AvatarSvg = u.Avatar,
                Words = words
            });
        }
        result.Entries = entries;
        return result;
    }

    private static string? Normalize(string? value)
    {
        var v = value?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    // "Aliya Karimova" -> "Aliya K."
    private static string DisplayName(string? first, string? last)
    {
        var f = first?.Trim() ?? "";
        var l = last?.Trim() ?? "";
        if (l.Length == 0) return f;
        return $"{f} {char.ToUpperInvariant(l[0])}.";
    }
}
