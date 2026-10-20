using Jumbo.Api.Data;
using Jumbo.Api.Services;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

[ApiController]
[Route("api/subjects")]
public class SubjectsController(AppDbContext db, SeedDataService seedDataService) : ControllerBase
{
    [HttpGet("book-numbers")]
    public async Task<ActionResult<List<int>>> GetBookNumbers() =>
        await db.Subjects.AsNoTracking().Select(s => s.BookId).Distinct().OrderBy(n => n).ToListAsync();

    [HttpGet("by-book/{bookNumber:int}")]
    public async Task<ActionResult<List<Subject>>> GetByBook(int bookNumber)
    {
        var subjects = await db.Subjects.AsNoTracking()
            .Where(s => s.BookId == bookNumber)
            .OrderBy(s => s.SequenceNumber)
            .ToListAsync();

        if (subjects.Count == 0)
        {
            await seedDataService.LoadBookDataAsync(bookNumber);
            subjects = await db.Subjects.AsNoTracking()
                .Where(s => s.BookId == bookNumber)
                .OrderBy(s => s.SequenceNumber)
                .ToListAsync();
        }

        return subjects;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Subject>> GetById(int id)
    {
        var subject = await db.Subjects.AsNoTracking().SingleOrDefaultAsync(s => s.SubjectId == id);
        if (subject is null) return NotFound();

        subject.VideoUrl = "https://www.youtube.com/watch?v=FHaObkHEkHQ";
        return subject;
    }

    [HttpGet("{id:int}/name")]
    public async Task<ActionResult<SubjectNameResponse>> GetName(int id)
    {
        var name = await db.Subjects.AsNoTracking().Where(s => s.SubjectId == id).Select(s => s.SubjectName).SingleOrDefaultAsync();
        return new SubjectNameResponse { Name = name ?? string.Empty };
    }
}
