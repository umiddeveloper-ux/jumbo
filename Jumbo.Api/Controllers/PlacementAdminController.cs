using Jumbo.Api.Auth;
using Jumbo.Api.Data;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

// The admin page of the level test: look at, edit, add, switch off and delete the questions and the reading texts, and upload the recorded
// voice of a listening question. Admin role only. The picture questions ("what is this?") are made from the books and are not here.
[ApiController]
[Route("api/admin/placement")]
[RequireRole(UserRole.Admin)]
public class PlacementAdminController(AppDbContext db) : ControllerBase
{
    private const int MaxAudioBytes = 5 * 1024 * 1024;
    private static readonly string[] Skills = [PlacementSkill.Vocabulary, PlacementSkill.Grammar, PlacementSkill.Reading, PlacementSkill.Listening];

    // ── questions ───────────────────────────────────────────────────────────
    [HttpGet("questions")]
    public async Task<List<PlacementAdminQuestionDto>> Questions()
    {
        var sizes = await db.PlacementAudios.AsNoTracking().Select(a => new { a.PlacementQuestionId, Size = a.Data.Length }).ToDictionaryAsync(a => a.PlacementQuestionId, a => a.Size);
        var titles = await db.PlacementPassages.AsNoTracking().ToDictionaryAsync(p => p.PlacementPassageId, p => p.Title);
        var rows = await db.PlacementQuestions.AsNoTracking().OrderBy(q => q.Level).ThenBy(q => q.Skill).ThenBy(q => q.Code).ToListAsync();
        return rows.Select(q => new PlacementAdminQuestionDto
        {
            Id = q.PlacementQuestionId, Code = q.Code, Skill = q.Skill, Level = q.Level, Prompt = q.Prompt, Options = q.Options, Answer = q.Answer,
            PassageId = q.PlacementPassageId, PassageTitle = q.PlacementPassageId is { } pid ? titles.GetValueOrDefault(pid) : null,
            Audio = q.Audio, IsActive = q.IsActive, AudioFileSize = sizes.TryGetValue(q.PlacementQuestionId, out var size) ? size : null
        }).ToList();
    }

    [HttpPost("questions")]
    public async Task<ActionResult<int>> CreateQuestion(SavePlacementQuestionRequest request)
    {
        var q = new PlacementQuestion { Code = "a-" + Guid.NewGuid().ToString("N")[..10] };
        if (await ApplyAsync(q, request) is { } error) return BadRequest(new { error });
        db.PlacementQuestions.Add(q);
        await db.SaveChangesAsync();
        return q.PlacementQuestionId;
    }

    [HttpPut("questions/{id:int}")]
    public async Task<IActionResult> UpdateQuestion(int id, SavePlacementQuestionRequest request)
    {
        var q = await db.PlacementQuestions.SingleOrDefaultAsync(x => x.PlacementQuestionId == id);
        if (q is null) return NotFound();
        if (await ApplyAsync(q, request) is { } error) return BadRequest(new { error });
        // a question that is no longer a listening question has no voice
        if (q.Skill != PlacementSkill.Listening) await db.PlacementAudios.Where(a => a.PlacementQuestionId == id).ExecuteDeleteAsync();
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("questions/{id:int}")]
    public async Task<IActionResult> DeleteQuestion(int id)
    {
        var n = await db.PlacementQuestions.Where(x => x.PlacementQuestionId == id).ExecuteDeleteAsync();
        return n == 0 ? NotFound() : NoContent();
    }

    // The checks of a question; returns the error code, or null when the question was filled in.
    private async Task<string?> ApplyAsync(PlacementQuestion q, SavePlacementQuestionRequest r)
    {
        if (!Skills.Contains(r.Skill)) return "BAD_SKILL";
        if (!PlacementScoring.Levels.Contains(r.Level)) return "BAD_LEVEL";

        var prompt = r.Prompt?.Trim() ?? string.Empty;
        if (prompt.Length == 0 || prompt.Length > 1000) return "BAD_PROMPT";

        var options = (r.Options ?? []).Select(o => o?.Trim() ?? string.Empty).ToList();
        if (options.Count is < 2 or > 6 || options.Any(o => o.Length == 0 || o.Length > 300)) return "BAD_OPTIONS";
        if (options.Select(o => o.ToLowerInvariant()).Distinct().Count() != options.Count) return "DUPLICATE_OPTIONS";
        if (r.Answer < 0 || r.Answer >= options.Count) return "BAD_ANSWER";

        var level = r.Level;
        int? passageId = null;
        if (r.Skill == PlacementSkill.Reading)
        {
            var passage = r.PassageId is { } pid ? await db.PlacementPassages.AsNoTracking().SingleOrDefaultAsync(p => p.PlacementPassageId == pid) : null;
            if (passage is null) return "BAD_PASSAGE";
            passageId = passage.PlacementPassageId;
            level = passage.Level;          // a question is served with the level of its text
        }

        var audio = (r.Audio ?? []).Select(l => l?.Trim() ?? string.Empty).Where(l => l.Length > 0).ToList();
        if (r.Skill == PlacementSkill.Listening)
        {
            if (audio.Count is < 1 or > 6 || audio.Any(l => l.Length > 500)) return "BAD_AUDIO_TEXT";
        }
        else audio = [];

        q.Skill = r.Skill;
        q.Level = level;
        q.Prompt = prompt;
        q.Options = options;
        q.Answer = r.Answer;
        q.PlacementPassageId = passageId;
        q.Audio = audio;
        q.IsActive = r.IsActive;
        return null;
    }

    // ── recorded voice of a listening question ──────────────────────────────
    [HttpPut("questions/{id:int}/audio")]
    [Consumes("application/octet-stream")]
    [RequestSizeLimit(MaxAudioBytes + 1024)]
    public async Task<IActionResult> UploadAudio(int id)
    {
        var q = await db.PlacementQuestions.AsNoTracking().SingleOrDefaultAsync(x => x.PlacementQuestionId == id);
        if (q is null) return NotFound();
        if (q.Skill != PlacementSkill.Listening) return BadRequest(new { error = "NOT_LISTENING" });

        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await Request.Body.ReadAsync(buffer)) > 0)
        {
            if (memory.Length + read > MaxAudioBytes) return BadRequest(new { error = "TOO_BIG" });
            memory.Write(buffer, 0, read);
        }
        var data = memory.ToArray();
        var type = Sniff(data);
        if (type is null) return BadRequest(new { error = "NOT_AUDIO" });

        var row = await db.PlacementAudios.SingleOrDefaultAsync(a => a.PlacementQuestionId == id);
        if (row is null) { row = new PlacementAudio { PlacementQuestionId = id }; db.PlacementAudios.Add(row); }
        row.ContentType = type;
        row.Data = data;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("questions/{id:int}/audio")]
    public async Task<IActionResult> DeleteAudio(int id)
    {
        await db.PlacementAudios.Where(a => a.PlacementQuestionId == id).ExecuteDeleteAsync();
        return NoContent();
    }

    // What the file really is, by its first bytes (the name and the header of the request are not trusted).
    private static string? Sniff(byte[] b)
    {
        if (b.Length < 12) return null;
        if (b[0] == 'I' && b[1] == 'D' && b[2] == '3') return "audio/mpeg";
        if (b[0] == 0xFF && (b[1] & 0xE0) == 0xE0) return "audio/mpeg";
        if (b[4] == 'f' && b[5] == 't' && b[6] == 'y' && b[7] == 'p') return "audio/mp4";
        if (b[0] == 'O' && b[1] == 'g' && b[2] == 'g' && b[3] == 'S') return "audio/ogg";
        if (b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'A' && b[10] == 'V' && b[11] == 'E') return "audio/wav";
        if (b[0] == 0x1A && b[1] == 0x45 && b[2] == 0xDF && b[3] == 0xA3) return "audio/webm";
        return null;
    }

    // ── reading texts ───────────────────────────────────────────────────────
    [HttpGet("passages")]
    public async Task<List<PlacementAdminPassageDto>> Passages()
    {
        var counts = await db.PlacementQuestions.AsNoTracking().Where(q => q.PlacementPassageId != null)
            .GroupBy(q => q.PlacementPassageId!.Value).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Id, g => g.Count);
        var rows = await db.PlacementPassages.AsNoTracking().OrderBy(p => p.Level).ThenBy(p => p.Code).ToListAsync();
        return rows.Select(p => new PlacementAdminPassageDto
        {
            Id = p.PlacementPassageId, Code = p.Code, Level = p.Level, Title = p.Title, Text = p.Text, QuestionCount = counts.GetValueOrDefault(p.PlacementPassageId)
        }).ToList();
    }

    [HttpPost("passages")]
    public async Task<ActionResult<int>> CreatePassage(SavePlacementPassageRequest request)
    {
        var p = new PlacementPassage { Code = "p-" + Guid.NewGuid().ToString("N")[..10] };
        if (CheckPassage(p, request) is { } error) return BadRequest(new { error });
        db.PlacementPassages.Add(p);
        await db.SaveChangesAsync();
        return p.PlacementPassageId;
    }

    // Changing the level of a text moves its questions to that level too.
    [HttpPut("passages/{id:int}")]
    public async Task<IActionResult> UpdatePassage(int id, SavePlacementPassageRequest request)
    {
        var p = await db.PlacementPassages.SingleOrDefaultAsync(x => x.PlacementPassageId == id);
        if (p is null) return NotFound();
        if (CheckPassage(p, request) is { } error) return BadRequest(new { error });
        await db.PlacementQuestions.Where(q => q.PlacementPassageId == id).ExecuteUpdateAsync(s => s.SetProperty(q => q.Level, p.Level));
        await db.SaveChangesAsync();
        return NoContent();
    }

    // The questions of the text are deleted with it.
    [HttpDelete("passages/{id:int}")]
    public async Task<IActionResult> DeletePassage(int id)
    {
        var n = await db.PlacementPassages.Where(x => x.PlacementPassageId == id).ExecuteDeleteAsync();
        return n == 0 ? NotFound() : NoContent();
    }

    private static string? CheckPassage(PlacementPassage p, SavePlacementPassageRequest r)
    {
        if (!PlacementScoring.Levels.Contains(r.Level)) return "BAD_LEVEL";
        var title = r.Title?.Trim() ?? string.Empty;
        var text = r.Text?.Trim() ?? string.Empty;
        if (title.Length == 0 || title.Length > 200) return "BAD_TITLE";
        if (text.Length == 0 || text.Length > 5000) return "BAD_TEXT";
        p.Level = r.Level;
        p.Title = title;
        p.Text = text;
        return null;
    }
}
