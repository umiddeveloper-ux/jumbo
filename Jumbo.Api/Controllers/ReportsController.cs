using Jumbo.Api.Data;
using Jumbo.Api.Services;
using Jumbo.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Jumbo.Api.Controllers;

// The public school report behind a secret link. Figures per team only: no pupil names ever leave this endpoint.
[ApiController]
[Route("api/reports")]
public class ReportsController(AppDbContext db, ReportService reports, IMemoryCache cache) : ControllerBase
{
    [HttpGet("{token}")]
    public async Task<ActionResult<SchoolReportDto>> School(string token, [FromQuery] string period = "week")
    {
        if (token.Length is < 20 or > 64) return NotFound();

        var link = await db.SchoolReportLinks.AsNoTracking().SingleOrDefaultAsync(l => l.Token == token && l.RevokedAt == null);
        if (link is null) return NotFound();

        period = string.Equals(period, "month", StringComparison.OrdinalIgnoreCase) ? "month" : "week";
        var key = $"school-report:{link.SchoolId}:{period}";
        if (!cache.TryGetValue(key, out SchoolReportDto? dto) || dto is null)
        {
            var school = await db.Schools.AsNoTracking().SingleAsync(s => s.SchoolId == link.SchoolId);
            dto = await reports.BuildSchoolAsync(school, period);
            cache.Set(key, dto, TimeSpan.FromSeconds(60));
        }
        return dto;
    }
}
