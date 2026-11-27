using Jumbo.Api.Data;
using Jumbo.Api.Services;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Jumbo.Api.Controllers;

// The public school reports behind a secret link. The activity report has figures per team only: no pupil names leave that endpoint.
// The exam report (/exams) is the director's: it lists the pupils of every team with their results, so it lives behind the same secret link.
[ApiController]
[Route("api/reports")]
public class ReportsController(AppDbContext db, ReportService reports, ExamReportService examReports, IMemoryCache cache) : ControllerBase
{
    [HttpGet("{token}")]
    public async Task<ActionResult<SchoolReportDto>> School(string token, [FromQuery] string period = "week")
    {
        var link = await ActiveLinkAsync(token);
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

    // The exams of the school by teams, with the names of the pupils. period = week | month | term (since 1 September); month by default.
    [HttpGet("{token}/exams")]
    public async Task<ActionResult<SchoolExamReportDto>> Exams(string token, [FromQuery] string? period = null)
    {
        var link = await ActiveLinkAsync(token);
        if (link is null) return NotFound();

        Response.Headers.CacheControl = "no-store";
        var key = ExamReportPeriods.Normalize(period);
        var cacheKey = $"school-exam-report:{link.SchoolId}:{key}";
        if (!cache.TryGetValue(cacheKey, out SchoolExamReportDto? dto) || dto is null)
        {
            var school = await db.Schools.AsNoTracking().SingleAsync(s => s.SchoolId == link.SchoolId);
            dto = await examReports.BuildSchoolAsync(school, key);
            cache.Set(cacheKey, dto, TimeSpan.FromSeconds(60));
        }
        return dto;
    }

    private async Task<SchoolReportLink?> ActiveLinkAsync(string token)
    {
        if (token.Length is < 20 or > 64) return null;
        return await db.SchoolReportLinks.AsNoTracking().SingleOrDefaultAsync(l => l.Token == token && l.RevokedAt == null);
    }
}
