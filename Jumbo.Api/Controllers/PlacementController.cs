using Jumbo.Api.Auth;
using Jumbo.Api.Data;
using Jumbo.Api.Services;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

// The level test is open to everybody (a visitor can try it before registering). A block has the right answers in it: the test only tells
// the learner where to start, nothing depends on it, and the scoring is shared code (Jumbo.Shared.PlacementScoring).
[ApiController]
[Route("api/placement")]
public class PlacementController(PlacementService placement, AppDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    // One block of the test: level = A1 | A2 | B1 | B2.
    [HttpGet("block/{level}")]
    public async Task<ActionResult<PlacementBlockDto>> Block(string level)
    {
        level = level.ToUpperInvariant();
        if (!PlacementService.IsLevel(level)) return NotFound();
        return await placement.BuildBlockAsync(level);
    }

    // The recorded voice of a listening question (the page plays it; the file only changes when the admin uploads another one).
    [HttpGet("audio/{id:int}")]
    public async Task<IActionResult> Audio(int id)
    {
        var audio = await db.PlacementAudios.AsNoTracking().SingleOrDefaultAsync(a => a.PlacementQuestionId == id);
        if (audio is null) return NotFound();
        Response.Headers.CacheControl = "public, max-age=3600";
        return File(audio.Data, audio.ContentType, lastModified: new DateTimeOffset(DateTime.SpecifyKind(audio.UpdatedAt, DateTimeKind.Utc)), entityTag: null, enableRangeProcessing: true);
    }

    // The most attempts one learner can store a day (the test is long; this only stops a script from filling the table).
    public const int MaxAttemptsPerDay = 20;

    // A signed-in learner has finished the test: the shares of right answers per block come in, the stage is worked out HERE (never taken from
    // the client) and stored. Teachers, directors and the admin look at the test but nothing is stored for them (204).
    [HttpPost("result")]
    [RequireAccessToken]
    public async Task<ActionResult<PlacementSavedDto>> SaveResult(SavePlacementResultRequest request)
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var role = await db.Users.AsNoTracking().Where(u => u.UserId == userId).Select(u => u.Role).SingleAsync();
        if (role != UserRole.Learner) return NoContent();

        var shares = request?.Shares;
        if (shares is null || shares.Count < 1 || shares.Count > PlacementScoring.Levels.Length || shares.Any(s => double.IsNaN(s) || s < 0 || s > 1))
            return BadRequest(new { error = "BAD_SHARES" });
        // a block follows only after a good one: anything else is not a test that was taken
        for (var i = 1; i < shares.Count; i++)
            if (!PlacementScoring.GoesOn(shares[i - 1])) return BadRequest(new { error = "BAD_SHARES" });

        var dayStart = DateTime.UtcNow.Date;
        if (await db.PlacementAttempts.CountAsync(r => r.UserId == userId && r.TakenAt >= dayStart) >= MaxAttemptsPerDay)
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "TOO_MANY" });

        var result = PlacementScoring.Evaluate(shares);
        var row = new PlacementAttempt
        {
            UserId = userId, TakenAt = DateTime.UtcNow, Stage = result.Stage, Level = result.Level, RecommendedBook = result.RecommendedBook,
            ShareA1 = shares.Count > 0 ? shares[0] : null, ShareA2 = shares.Count > 1 ? shares[1] : null,
            ShareB1 = shares.Count > 2 ? shares[2] : null, ShareB2 = shares.Count > 3 ? shares[3] : null,
        };
        db.PlacementAttempts.Add(row);
        await db.SaveChangesAsync();
        return PlacementHistory.Saved(row);
    }

    // The signed-in learner's latest attempt and how many he has made.
    [HttpGet("my")]
    [RequireAccessToken]
    public async Task<ActionResult<MyPlacementDto>> My()
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var latest = await db.PlacementAttempts.AsNoTracking().Where(r => r.UserId == userId)
            .OrderByDescending(r => r.TakenAt).ThenByDescending(r => r.PlacementAttemptId).FirstOrDefaultAsync();
        var count = await db.PlacementAttempts.CountAsync(r => r.UserId == userId);
        return new MyPlacementDto { Latest = latest is null ? null : PlacementHistory.Saved(latest), Count = count };
    }
}
