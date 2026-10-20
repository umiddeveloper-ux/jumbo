using Jumbo.Api.Data;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

/// <summary>The apps ask here, at their start, whether a newer version is out (and whether theirs may still be used). Open to everybody.</summary>
[ApiController]
[Route("api/app")]
public class AppVersionController(AppDbContext db) : ControllerBase
{
    public static readonly string[] Platforms = ["android", "ios", "windows"];

    [HttpGet("version")]
    public async Task<ActionResult<AppVersionDto>> Get([FromQuery] string platform)
    {
        platform = (platform ?? string.Empty).Trim().ToLowerInvariant();
        if (!Platforms.Contains(platform)) return BadRequest(new { error = "UNKNOWN_PLATFORM" });
        var row = await db.AppVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Platform == platform);
        Response.Headers.CacheControl = "no-store";
        return row is null ? new AppVersionDto { Platform = platform } : ToDto(row);      // no row = nothing to offer
    }

    public static AppVersionDto ToDto(AppVersion r) => new()
    {
        Platform = r.Platform, LatestCode = r.LatestCode, MinCode = r.MinCode, Url = r.Url, NotesRu = r.NotesRu, NotesEn = r.NotesEn, NotesUz = r.NotesUz
    };
}
