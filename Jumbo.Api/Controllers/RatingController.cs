using Jumbo.Api.Auth;
using Jumbo.Api.Data;
using Jumbo.Api.Services;
using Jumbo.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Jumbo.Api.Controllers;

// Leaderboards. Scopes: the caller's own team / school / region, or everybody (see RatingService).
[ApiController]
[Route("api/rating")]
public class RatingController(AppDbContext db, ICurrentUserService currentUser, RatingService ratings, IMemoryCache cache) : ControllerBase
{
    // Signed-in view: any scope, and the caller's own place ("Me").
    [HttpGet]
    [RequireAccessToken]
    public async Task<ActionResult<RatingDto>> Get([FromQuery] string scope = "all", [FromQuery] string period = "week", [FromQuery] int take = 50)
    {
        scope = scope.ToLowerInvariant();
        period = period.ToLowerInvariant();
        if (scope is not ("team" or "school" or "region" or "all")) return BadRequest(new { error = "Unknown scope." });
        if (period is not ("week" or "all")) return BadRequest(new { error = "Unknown period." });

        var meId = (await currentUser.GetUserIdAsync())!.Value;
        var me = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == meId);
        return await ratings.GetAsync(me, scope, period, take);
    }

    // Public top list for the website's home page (visitors who are not signed in). Same privacy rules as
    // the signed-in view ("First L.", team, preset avatar; hidden players are anonymous). Cached for a minute
    // because the landing page can be opened by anyone.
    [HttpGet("top")]
    public async Task<ActionResult<RatingDto>> Top([FromQuery] string period = "week", [FromQuery] int take = 10)
    {
        period = period.ToLowerInvariant();
        if (period is not ("week" or "all")) return BadRequest(new { error = "Unknown period." });
        take = Math.Clamp(take, 1, 20);

        var key = $"rating-top:{period}:{take}";
        if (!cache.TryGetValue(key, out RatingDto? dto) || dto is null)
        {
            dto = await ratings.GetAsync(null, "all", period, take);
            cache.Set(key, dto, TimeSpan.FromSeconds(60));
        }
        return dto;
    }
}
