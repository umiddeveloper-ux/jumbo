using Jumbo.Api.Services;
using Jumbo.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Jumbo.Api.Controllers;

// The level test is open to everybody (a visitor can try it before registering). A block has the right answers in it: the test only tells
// the learner where to start, nothing depends on it, and the scoring is shared code (Jumbo.Shared.PlacementScoring).
[ApiController]
[Route("api/placement")]
public class PlacementController(PlacementService placement) : ControllerBase
{
    // One block of the test: level = A1 | A2 | B1 | B2.
    [HttpGet("block/{level}")]
    public async Task<ActionResult<PlacementBlockDto>> Block(string level)
    {
        level = level.ToUpperInvariant();
        if (!PlacementService.IsLevel(level)) return NotFound();
        return await placement.BuildBlockAsync(level);
    }
}
