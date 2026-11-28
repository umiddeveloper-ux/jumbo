using Jumbo.Api.Auth;
using Jumbo.Api.Data;
using Jumbo.Api.Services;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

[ApiController]
[Route("api/progress")]
[RequireAccessToken]
public class ProgressController(AppDbContext db, ICurrentUserService currentUser, DailyProgressService daily, Jumbo.Api.Services.LivesService lives) : ControllerBase
{
    [HttpGet("words")]
    public async Task<ActionResult<List<UsersWord>>> GetWords()
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        return await db.UsersWords.AsNoTracking().Where(uw => uw.UserId == userId).ToListAsync();
    }

    [HttpPost("words/ensure")]
    public async Task<IActionResult> EnsureWords(EnsureWordsRequest request)
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;

        var existingIds = await db.UsersWords.AsNoTracking()
            .Where(uw => uw.UserId == userId)
            .Select(uw => uw.WordId)
            .ToListAsync();
        var existingSet = existingIds.ToHashSet();

        foreach (var wordId in request.WordIds)
        {
            if (!existingSet.Contains(wordId))
                db.UsersWords.Add(new UsersWord { UserId = userId, WordId = wordId, RepeatingCount = 0 });
        }

        await db.SaveChangesAsync();

        // A game was just won with these words: count them towards today's goal / streak.
        return Ok(await daily.RecordWordsAsync(userId, request.WordIds));
    }

    [HttpGet("daily")]
    public async Task<ActionResult<DailyProgressDto>> GetDaily()
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        return await daily.GetAsync(userId);
    }

    [HttpPost("words/{wordId:int}/increment")]
    public async Task<ActionResult<UsersWord>> IncrementRepetition(int wordId)
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;

        var existing = await db.UsersWords.SingleOrDefaultAsync(uw => uw.UserId == userId && uw.WordId == wordId);
        if (existing is not null)
        {
            existing.RepeatingCount++;
        }
        else
        {
            existing = new UsersWord { UserId = userId, WordId = wordId, RepeatingCount = 1 };
            db.UsersWords.Add(existing);
        }

        await db.SaveChangesAsync();
        await daily.RecordWordsAsync(userId, [wordId]);
        return existing;
    }

    // The books the learner may open: book 1; every book up to the one they already play in (nobody who started before the
    // check existed is locked out); every book opened by passing the 3-step check over the previous one.
    [HttpGet("books/unlocked")]
    public async Task<ActionResult<List<int>>> UnlockedBooks()
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        return await UnlockedAsync(userId);
    }

    // Called by the website when the learner has passed the three steps over book (n - 1). The previous book must be open.
    [HttpPost("books/{bookNumber:int}/unlock")]
    public async Task<IActionResult> UnlockBook(int bookNumber)
    {
        if (bookNumber < 2 || bookNumber > Jumbo.Shared.Course.BookCount) return BadRequest(new { error = "INVALID_BOOK" });

        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var open = await UnlockedAsync(userId);
        if (open.Contains(bookNumber)) return NoContent();
        if (!open.Contains(bookNumber - 1)) return Conflict(new { error = "PREVIOUS_LOCKED" });

        db.UserBookUnlocks.Add(new UserBookUnlock { UserId = userId, BookNumber = bookNumber });
        await db.SaveChangesAsync();
        return NoContent();
    }

    // The book the learner is in: the first open book that still has a topic with a game not won (the last open one when all are won).
    [HttpGet("current-book")]
    public async Task<ActionResult<int>> CurrentBook()
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        return await BookProgress.CurrentBookAsync(db, userId);
    }

    // The numbers of the "welcome back" dialog (language, percent of the course, the book the learner is in). 204 for a teacher, a director and the admin.
    [HttpGet("summary")]
    public async Task<ActionResult<LearningSummaryDto>> Summary([FromServices] LearningSummaryService summary)
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var result = await summary.BuildAsync(userId);
        if (result is null) return NoContent();
        Response.Headers.CacheControl = "no-store";
        return result;
    }

    private Task<List<int>> UnlockedAsync(int userId) => BookProgress.UnlockedAsync(db, userId);

    [HttpGet("subjects")]
    public async Task<ActionResult<List<UserSubjectProgress>>> GetSubjects()
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        return await db.UserSubjectProgresses.AsNoTracking().Where(p => p.UserId == userId).ToListAsync();
    }

    [HttpGet("subjects/{subjectId:int}")]
    public async Task<ActionResult<UserSubjectProgress>> GetSubject(int subjectId)
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var progress = await db.UserSubjectProgresses.AsNoTracking()
            .SingleOrDefaultAsync(p => p.UserId == userId && p.SubjectId == subjectId);
        return progress is null ? NotFound() : progress;
    }

    [HttpPost("subjects/{subjectId:int}/win")]
    public async Task<ActionResult<LivesStateDto>> MarkWon(int subjectId, MarkGameWonRequest request)
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;

        var record = await db.UserSubjectProgresses.SingleOrDefaultAsync(p => p.UserId == userId && p.SubjectId == subjectId);
        if (record is null)
        {
            record = new UserSubjectProgress { UserId = userId, SubjectId = subjectId };
            db.UserSubjectProgresses.Add(record);
        }

        switch (request.GameType)
        {
            case "FindMe": record.FindMeWon = true; break;
            case "Sequence": record.SequenceWon = true; break;
            case "ListenAndFind": record.ListenWon = true; break;
            case "Steps": record.StepsWon = true; break;
            case "Phrase": record.PhraseWon = true; break;
        }

        await db.SaveChangesAsync();

        // The game is won: the place the learner had saved in it is not needed any more (he starts a new round next time).
        if (GameProgressRules.KeyOfGameType(request.GameType) is { } savedKey)
            await db.GameProgresses.Where(g => g.UserId == userId && g.Game == savedKey && g.SubjectId == subjectId).ExecuteDeleteAsync();

        // The LP for the win are given here (by the game, with the limits of LivesService): the clients do not send a number of LP.
        // The answer is the learner's numbers now.
        return await lives.AwardGameWinAsync(userId, request.GameType);
    }
}
