using Jumbo.Api.Auth;
using Jumbo.Api.Data;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Jumbo.Api.Controllers;

// The games of a topic that a learner left half way (Find me, Listen, Phrase, Sequence, Steps): the place he stopped at is kept so that he can go on at
// the next visit, on any device. Learners only: for a teacher, a director or the admin nothing is stored (204), they only look at the games. The state is
// an opaque JSON text of the game; the server keeps it small and valid and never trusts it for anything but giving it back to the same learner.
[ApiController]
[Route("api/games/progress")]
[RequireAccessToken]
public class GameProgressController(AppDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    private async Task<(int UserId, bool Learner)> WhoAsync()
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var role = await db.Users.AsNoTracking().Where(u => u.UserId == userId).Select(u => u.Role).SingleAsync();
        return (userId, role == UserRole.Learner);
    }

    // Saves (or replaces) the saved game. One statement: it is called after every answer of a game.
    [HttpPut]
    public async Task<IActionResult> Save(SaveGameProgressRequest request)
    {
        if (request is null || !GameProgressRules.IsGame(request.Game) || request.SubjectId <= 0) return BadRequest(new { error = "BAD_GAME" });
        if (!GameProgressRules.IsValidProgress(request.Done, request.Total)) return BadRequest(new { error = "BAD_PROGRESS" });
        if (!GameProgressRules.IsValidState(request.State)) return BadRequest(new { error = "BAD_STATE" });

        var (userId, learner) = await WhoAsync();
        if (!learner) return NoContent();

        var now = DateTime.UtcNow;
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO game_progress (user_id, game, subject_id, state, done, total, updated_at)
                VALUES ({userId}, {request.Game}, {request.SubjectId}, {request.State}, {request.Done}, {request.Total}, {now})
                ON CONFLICT (user_id, game, subject_id)
                DO UPDATE SET state = EXCLUDED.state, done = EXCLUDED.done, total = EXCLUDED.total, updated_at = EXCLUDED.updated_at");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            return BadRequest(new { error = "BAD_SUBJECT" });          // no such topic
        }

        // now and then the saved games nobody came back to are removed (cheap: the index is on the date)
        if (Random.Shared.Next(100) == 0)
        {
            var cutoff = GameProgressRules.ExpiresBefore(now);
            await db.GameProgresses.Where(g => g.UpdatedAt < cutoff).ExecuteDeleteAsync();
        }
        return NoContent();
    }

    // The saved game of one game in one topic with its state; 204 when there is none (or it is older than two weeks).
    [HttpGet]
    public async Task<ActionResult<GameProgressDto>> Get([FromQuery] string game, [FromQuery] int subjectId)
    {
        if (!GameProgressRules.IsGame(game) || subjectId <= 0) return BadRequest(new { error = "BAD_GAME" });
        var (userId, learner) = await WhoAsync();
        if (!learner) return NoContent();

        var cutoff = GameProgressRules.ExpiresBefore(DateTime.UtcNow);
        var row = await db.GameProgresses.AsNoTracking()
            .Where(g => g.UserId == userId && g.Game == game && g.SubjectId == subjectId && g.UpdatedAt >= cutoff)
            .Select(g => new GameProgressDto { Game = g.Game, SubjectId = g.SubjectId, State = g.State, Done = g.Done, Total = g.Total, UpdatedAt = g.UpdatedAt })
            .SingleOrDefaultAsync();
        if (row is null) return NoContent();
        row.UpdatedAt = DateTime.SpecifyKind(row.UpdatedAt, DateTimeKind.Utc);
        return row;
    }

    // All the learner's saved games without their state (the badges), the newest first. The ones older than two weeks are removed here.
    [HttpGet("unfinished")]
    public async Task<ActionResult<List<GameProgressSummaryDto>>> Unfinished()
    {
        var (userId, learner) = await WhoAsync();
        if (!learner) return new List<GameProgressSummaryDto>();

        var cutoff = GameProgressRules.ExpiresBefore(DateTime.UtcNow);
        await db.GameProgresses.Where(g => g.UserId == userId && g.UpdatedAt < cutoff).ExecuteDeleteAsync();

        var rows = await db.GameProgresses.AsNoTracking().Where(g => g.UserId == userId)
            .OrderByDescending(g => g.UpdatedAt)
            .Select(g => new GameProgressSummaryDto { Game = g.Game, SubjectId = g.SubjectId, Done = g.Done, Total = g.Total, UpdatedAt = g.UpdatedAt })
            .ToListAsync();
        foreach (var r in rows) r.UpdatedAt = DateTime.SpecifyKind(r.UpdatedAt, DateTimeKind.Utc);
        return rows;
    }

    // The learner starts over, or lost the game: the saved game is gone.
    [HttpDelete]
    public async Task<IActionResult> Delete([FromQuery] string game, [FromQuery] int subjectId)
    {
        if (!GameProgressRules.IsGame(game) || subjectId <= 0) return BadRequest(new { error = "BAD_GAME" });
        var (userId, learner) = await WhoAsync();
        if (!learner) return NoContent();

        await db.GameProgresses.Where(g => g.UserId == userId && g.Game == game && g.SubjectId == subjectId).ExecuteDeleteAsync();
        return NoContent();
    }
}
