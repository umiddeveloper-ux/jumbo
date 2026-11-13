using Jumbo.Api.Data;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

[ApiController]
[Route("api/languages")]
public class LanguagesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Language>>> GetAll() =>
        await db.Languages.AsNoTracking().OrderBy(l => l.LanguageId).ToListAsync();

    [HttpGet("by-code/{shortName}")]
    public async Task<ActionResult<Language>> GetByShortName(string shortName)
    {
        var language = await db.Languages.AsNoTracking().SingleOrDefaultAsync(l => l.ShorName == shortName);
        return language is null ? NotFound() : language;
    }
}
