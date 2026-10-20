using Jumbo.Api.Auth;
using Jumbo.Api.Data;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

/// <summary>
/// The market: what the learner buys with LP.
///   things that are used up (life, refill, shield, freeze) work at once and can be bought again;
///   things that are worn (frame, avatar, badge, theme, pet) are bought once, put on and taken off.
/// The rows of an item are per language (same picture): what the learner owns is found by the picture.
/// </summary>
[ApiController]
[Route("api/market")]
[RequireAccessToken]
public class MarketController(AppDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    public const int MaxLives = 5, MaxShields = 3, MaxFreezes = 2;

    private static readonly string[] Worn = ["frame", "avatar", "badge", "theme", "pet"];

    [HttpGet]
    public async Task<ActionResult<List<MarketItem>>> GetAll([FromQuery] int languageId)
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == userId);

        var ownedSvg = (await (from um in db.UserMarkets.AsNoTracking()
                               where um.UserId == userId
                               join m in db.Markets.AsNoTracking() on um.MarketId equals m.MarketId
                               select m.SvgImageId).ToListAsync()).ToHashSet();

        var markets = await db.Markets.AsNoTracking()
            .Where(m => m.LanguageId == languageId && m.Kind != "" && m.Kind != "hidden")
            .OrderBy(m => m.Price).ThenBy(m => m.MarketId)
            .ToListAsync();
        var svgIds = markets.Select(m => m.SvgImageId).ToList();
        var svgLookup = await db.SvgImages.AsNoTracking()
            .Where(s => svgIds.Contains(s.SvgImageId))
            .ToDictionaryAsync(s => s.SvgImageId, s => s.SVG);

        return markets.Select(m =>
        {
            bool worn = Worn.Contains(m.Kind);
            bool owned = worn && ownedSvg.Contains(m.SvgImageId);
            return new MarketItem
            {
                Id = m.MarketId,
                Name = m.Name,
                SvgImage = svgLookup.GetValueOrDefault(m.SvgImageId, string.Empty),
                Price = m.Price,
                Kind = m.Kind,
                Key = m.Key,
                Owned = owned,
                Equipped = owned && WornKey(user, m.Kind) == m.Key,
                CanBuy = !owned && user.LpPoints >= m.Price && Limit(user, m.Kind) is null
            };
        }).ToList();
    }

    [HttpPost("{marketId:int}/purchase")]
    public async Task<ActionResult<PurchaseResponse>> Purchase(int marketId)
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var user = await db.Users.SingleAsync(u => u.UserId == userId);
        var market = await db.Markets.AsNoTracking().SingleOrDefaultAsync(m => m.MarketId == marketId);
        if (market is null || market.Kind is "" or "hidden") return NotFound();

        bool worn = Worn.Contains(market.Kind);
        if (worn && await Owns(userId, market.SvgImageId)) return BadRequest(new { error = "OWNED" });
        if (user.LpPoints < market.Price) return BadRequest(new { error = "NOT_ENOUGH" });
        if (Limit(user, market.Kind) is { } limit) return BadRequest(new { error = limit });

        // LP are the reserve that keeps the 5-hour block away: a purchase must not leave a learner with neither lives nor LP.
        int livesAfter = market.Kind == "refill" ? MaxLives : market.Kind == "life" ? user.LifeCount + 1 : user.LifeCount;
        if (livesAfter <= 0 && user.LpPoints - market.Price <= 0) return BadRequest(new { error = "WOULD_BLOCK" });

        user.LpPoints -= market.Price;
        switch (market.Kind)
        {
            case "life":
                user.LifeCount++;
                break;
            case "refill":
                user.LifeCount = MaxLives;
                break;
            case "shield":
                user.ShieldCount++;
                break;
            case "freeze":
                user.FreezeCount++;
                break;
            default:
                db.UserMarkets.Add(new UserMarkets { UserId = userId, MarketId = marketId });
                await Wear(user, market);                      // a worn thing is worn as soon as it is bought
                break;
        }
        // lives are back: the 5-hour clock is not needed
        if (user.LifeCount > 0) user.LifeExpirationDate = default;

        await db.SaveChangesAsync();
        return Respond(user, equipped: worn);
    }

    /// <summary>Puts an owned thing on.</summary>
    [HttpPost("{marketId:int}/equip")]
    public async Task<ActionResult<PurchaseResponse>> Equip(int marketId)
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var user = await db.Users.SingleAsync(u => u.UserId == userId);
        var market = await db.Markets.AsNoTracking().SingleOrDefaultAsync(m => m.MarketId == marketId);
        if (market is null || !Worn.Contains(market.Kind)) return NotFound();
        if (!await Owns(userId, market.SvgImageId)) return BadRequest(new { error = "NOT_OWNED" });

        await Wear(user, market);
        await db.SaveChangesAsync();
        return Respond(user, equipped: true);
    }

    /// <summary>Takes a worn thing off (kind: frame | avatar | badge | theme | pet).</summary>
    [HttpPost("unequip/{kind}")]
    public async Task<ActionResult<PurchaseResponse>> Unequip(string kind)
    {
        if (!Worn.Contains(kind)) return NotFound();
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var user = await db.Users.SingleAsync(u => u.UserId == userId);

        switch (kind)
        {
            case "frame": user.FrameKey = null; break;
            case "badge": user.BadgeKey = null; break;
            case "theme": user.ThemeKey = null; break;
            case "pet": user.PetKey = null; break;
            case "avatar":
                if (user.AvatarItemKey is not null)
                {
                    user.AvatarSvg = user.AvatarDefaultSvg;     // the learner's own avatar is back
                    user.AvatarItemKey = null;
                    user.AvatarDefaultSvg = null;
                }
                break;
        }
        await db.SaveChangesAsync();
        return Respond(user, equipped: false);
    }

    private async Task<bool> Owns(int userId, int svgImageId) =>
        await (from um in db.UserMarkets
               where um.UserId == userId
               join m in db.Markets on um.MarketId equals m.MarketId
               select m.SvgImageId).AnyAsync(id => id == svgImageId);

    private async Task Wear(User user, Market market)
    {
        switch (market.Kind)
        {
            case "frame": user.FrameKey = market.Key; break;
            case "badge": user.BadgeKey = market.Key; break;
            case "theme": user.ThemeKey = market.Key; break;
            case "pet": user.PetKey = market.Key; break;
            case "avatar":
                var svg = await db.SvgImages.AsNoTracking().Where(s => s.SvgImageId == market.SvgImageId).Select(s => s.SVG).SingleAsync();
                if (user.AvatarItemKey is null) user.AvatarDefaultSvg = user.AvatarSvg;     // keep the learner's own
                user.AvatarSvg = svg;
                user.AvatarItemKey = market.Key;
                break;
        }
    }

    private static string? WornKey(User user, string kind) => kind switch
    {
        "frame" => user.FrameKey,
        "badge" => user.BadgeKey,
        "theme" => user.ThemeKey,
        "pet" => user.PetKey,
        "avatar" => user.AvatarItemKey,
        _ => null
    };

    // Why a thing that is used up cannot be bought now (null = it can).
    private static string? Limit(User user, string kind) => kind switch
    {
        "life" or "refill" when user.LifeCount >= MaxLives => "LIVES_FULL",
        "shield" when user.ShieldCount >= MaxShields => "LIMIT",
        "freeze" when user.FreezeCount >= MaxFreezes => "LIMIT",
        _ => null
    };

    private static PurchaseResponse Respond(User user, bool equipped) => new()
    {
        LpPoints = user.LpPoints,
        LifeCount = user.LifeCount,
        ShieldCount = user.ShieldCount,
        FreezeCount = user.FreezeCount,
        Equipped = equipped
    };
}
