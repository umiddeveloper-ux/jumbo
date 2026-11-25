using Jumbo.Api.Data;
using Jumbo.Api.Services;
using Jumbo.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

// The level test is open to everybody (a visitor can try it before registering). A block has the right answers in it: the test only tells
// the learner where to start, nothing depends on it, and the scoring is shared code (Jumbo.Shared.PlacementScoring).
[ApiController]
[Route("api/placement")]
public class PlacementController(PlacementService placement, AppDbContext db) : ControllerBase
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
}
