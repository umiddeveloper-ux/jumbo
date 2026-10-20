using Jumbo.Api.Data;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

// The pick lists of the registration wizard and the profile: verified schools of a region, active teams of a school.
// Public on purpose (the account does not exist yet while registering); only names are exposed.
[ApiController]
[Route("api/directory")]
public class DirectoryController(AppDbContext db) : ControllerBase
{
    [HttpGet("schools")]
    public async Task<ActionResult<List<SchoolDto>>> Schools([FromQuery] string region)
    {
        if (!RegionCodes.IsValid(region)) return new List<SchoolDto>();
        return await db.Schools.AsNoTracking()
            .Where(s => s.Region == region && s.IsVerified)
            .OrderBy(s => s.Name)
            .Select(s => new SchoolDto { SchoolId = s.SchoolId, Region = s.Region, Name = s.Name, IsVerified = true })
            .ToListAsync();
    }

    [HttpGet("schools/{schoolId:int}/teams")]
    public async Task<ActionResult<List<TeamOptionDto>>> Teams(int schoolId)
    {
        return await db.SchoolClasses.AsNoTracking()
            .Where(c => c.SchoolId == schoolId && !c.IsArchived)
            .OrderBy(c => c.Name)
            .Select(c => new TeamOptionDto { ClassId = c.ClassId, Name = c.Name })
            .ToListAsync();
    }
}
