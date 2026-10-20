using Jumbo.Api.Data;
using Jumbo.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

[ApiController]
[Route("api/svg-images")]
public class SvgImagesController(AppDbContext db) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<SvgImageResponse>> GetById(int id)
    {
        var svg = await db.SvgImages.AsNoTracking().Where(s => s.SvgImageId == id).Select(s => s.SVG).SingleOrDefaultAsync();
        return new SvgImageResponse { Svg = svg ?? string.Empty };
    }
}
