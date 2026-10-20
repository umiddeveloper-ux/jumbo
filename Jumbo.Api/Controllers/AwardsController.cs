using Jumbo.Api.Auth;
using Jumbo.Api.Data;
using Jumbo.Api.Services;
using Jumbo.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

[ApiController]
[Route("api/awards")]
[RequireAccessToken]
public class AwardsController(AppDbContext db, ICurrentUserService currentUser, AwardService awardService) : ControllerBase
{
    /// <summary>The awards the learner has earned since they last asked (each is returned once): for the pop-up "you got an award".</summary>
    [HttpGet("new")]
    public async Task<ActionResult<List<AwardItem>>> New([FromQuery] int languageId)
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        return await awardService.TakeNewAsync(userId, languageId);
    }

    [HttpGet]
    public async Task<ActionResult<List<AwardItem>>> GetAll([FromQuery] int languageId)
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;

        await awardService.EvaluateAsync(userId);

        // An award is stored once (on the English row); the other languages have rows with the same picture.
        var achievedSet = (await (from ua in db.UserAwards.AsNoTracking()
                                  where ua.UserId == userId
                                  join a in db.Awards.AsNoTracking() on ua.AwardsId equals a.AwardId
                                  select a.SvgImageId).ToListAsync()).ToHashSet();

        var awards = await db.Awards.AsNoTracking().Where(a => a.LanguageId == languageId).ToListAsync();
        var svgIds = awards.Select(a => a.SvgImageId).ToList();
        var svgLookup = await db.SvgImages.AsNoTracking()
            .Where(s => svgIds.Contains(s.SvgImageId))
            .ToDictionaryAsync(s => s.SvgImageId, s => s.SVG);

        return awards.Select(a => new AwardItem
        {
            Title = a.AwardName,
            SvgImage = svgLookup.GetValueOrDefault(a.SvgImageId, string.Empty),
            IsAchieved = achievedSet.Contains(a.SvgImageId)
        }).ToList();
    }
}
