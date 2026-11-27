using Jumbo.Api.Data;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

[ApiController]
[Route("api/svg-images")]
public class SvgImagesController(AppDbContext db, IConfiguration config) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<SvgImageResponse>> GetById(int id, [FromQuery] bool withSvg = true)
    {
        // Pictures:OmitSvg / withSvg=false: the text of a picture that has a file is not sent (for clients that use ImageUrl; the old apps need the text)
        var omitSvg = !withSvg || config.GetValue<bool>("Pictures:OmitSvg");
        var picture = await db.SvgImages.AsNoTracking()
            .Where(s => s.SvgImageId == id)
            .Select(s => new { Svg = omitSvg && s.ImageFile != null ? string.Empty : s.SVG, s.ImageFile })
            .SingleOrDefaultAsync();
        return new SvgImageResponse { Svg = picture?.Svg ?? string.Empty, ImageUrl = PictureFiles.UrlOf(picture?.ImageFile) };
    }
}
