using Jumbo.Api.Auth;
using Jumbo.Api.Data;
using Jumbo.Api.Services;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

[ApiController]
[Route("api/words")]
public class WordsController(AppDbContext db, SeedDataService seedDataService, ICurrentUserService currentUser) : ControllerBase
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    [HttpGet]
    public async Task<ActionResult<List<Word>>> GetPaged([FromQuery] string? filterText, [FromQuery] int page, [FromQuery] int pageSize, [FromQuery] int languageId)
    {
        var query = db.Words.AsNoTracking().Where(w => w.LanguageId == languageId);

        // The words are numbered like in the printed books (1..4700, the same number in every language): typing a
        // number ("16", "#16", "№16") finds that word of the book.
        var numberText = filterText?.Trim().TrimStart('#', '№', 'N', 'n', ' ');
        if (!string.IsNullOrEmpty(numberText) && numberText.All(char.IsAsciiDigit) && numberText.Length <= 5)
        {
            var number = int.Parse(numberText);
            return await query.Where(w => w.WordNumber == number).OrderBy(w => w.WordId)
                .Skip(page * pageSize).Take(pageSize).ToListAsync();
        }

        if (!string.IsNullOrWhiteSpace(filterText))
        {
            var filter = filterText.ToLower();
            query = query.Where(w => w.WordName.ToLower().Contains(filter));
        }

        return await query.OrderBy(w => w.WordName).ThenBy(w => w.WordId)
            .Skip(page * pageSize).Take(pageSize).ToListAsync();
    }

    [HttpGet("count")]
    public async Task<ActionResult<int>> GetCount([FromQuery] int languageId) =>
        await db.Words.AsNoTracking().CountAsync(w => w.LanguageId == languageId);

    [HttpGet("by-letter")]
    public async Task<ActionResult<List<Word>>> GetByLetter([FromQuery] string letter, [FromQuery] int languageId, [FromQuery] int count)
    {
        var upper = letter.ToUpper();
        return await db.Words.AsNoTracking()
            .Where(w => w.LanguageId == languageId && w.WordName.ToUpper().StartsWith(upper))
            .Take(count)
            .ToListAsync();
    }

    [HttpGet("by-subject")]
    public async Task<ActionResult<List<Word>>> GetBySubject([FromQuery] int subjectId, [FromQuery] int languageId, [FromQuery] int count = 12)
    {
        return await db.Words.AsNoTracking()
            .Where(w => w.SubjectId == subjectId && w.LanguageId == languageId)
            .Take(count)
            .ToListAsync();
    }

    // A random sample from every topic of a book (the check that opens the next book).
    [HttpGet("by-book")]
    public async Task<ActionResult<List<Word>>> GetByBook([FromQuery] int bookNumber, [FromQuery] int languageId, [FromQuery] int count = 12)
    {
        count = Math.Clamp(count, 1, 60);
        var subjectIds = db.Subjects.AsNoTracking().Where(s => s.BookId == bookNumber).Select(s => s.SubjectId);
        return await db.Words.AsNoTracking()
            .Where(w => subjectIds.Contains(w.SubjectId) && w.LanguageId == languageId)
            .OrderBy(_ => EF.Functions.Random())
            .Take(count)
            .ToListAsync();
    }

    // The words of a topic with their translation and picture in one answer: a game used to ask for them one by one (about 30 requests).
    [HttpGet("by-subject/cards")]
    public async Task<ActionResult<List<WordCardDto>>> GetCardsBySubject([FromQuery] int subjectId, [FromQuery] int languageId, [FromQuery] int translationLanguageId)
    {
        return await (
            from w in db.Words.AsNoTracking().Where(w => w.SubjectId == subjectId && w.LanguageId == languageId)
            join t in db.Words.AsNoTracking().Where(x => x.LanguageId == translationLanguageId) on w.SvgImageId equals t.SvgImageId into translations
            from t in translations.DefaultIfEmpty()
            join s in db.SvgImages.AsNoTracking() on w.SvgImageId equals s.SvgImageId into pictures
            from s in pictures.DefaultIfEmpty()
            orderby w.WordNumber, w.WordId
            select new WordCardDto
            {
                WordId = w.WordId,
                SubjectId = w.SubjectId,
                SvgImageId = w.SvgImageId,
                WordName = w.WordName,
                Example = w.Example,
                TranslationWordId = t == null ? 0 : t.WordId,
                TranslationWord = t == null ? null : t.WordName,
                TranslationExample = t == null ? null : t.Example,
                Svg = s == null ? string.Empty : s.SVG
            }).ToListAsync();
    }

    [HttpGet("by-subject/all")]
    public async Task<ActionResult<List<Word>>> GetAllBySubject([FromQuery] int subjectId, [FromQuery] int languageId)
    {
        var words = await db.Words.AsNoTracking()
            .Where(w => w.SubjectId == subjectId && w.LanguageId == languageId)
            .ToListAsync();

        if (words.Count == 0)
        {
            await seedDataService.FixSubjectWordsAsync(subjectId);
            words = await db.Words.AsNoTracking()
                .Where(w => w.SubjectId == subjectId && w.LanguageId == languageId)
                .ToListAsync();
        }

        return words;
    }

    [HttpGet("by-svg")]
    public async Task<ActionResult<Word>> GetBySvg([FromQuery] int svgImageId, [FromQuery] int languageId)
    {
        var word = await db.Words.AsNoTracking().SingleOrDefaultAsync(w => w.SvgImageId == svgImageId && w.LanguageId == languageId);
        return word is null ? NotFound() : word;
    }

    // The exam is only for players who have told us their region, school and team (profile): the data
    // behind the reports for teachers and school principals.
    [HttpGet("exam")]
    [RequireAccessToken]
    public async Task<ActionResult<List<ExamWordItem>>> GetExamWords(
        [FromQuery] int bookNumber, [FromQuery] int studyLanguageId,
        [FromQuery] int transLanguageId, [FromQuery] int count)
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var profile = await db.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new { u.Region, u.School, u.Team })
            .SingleAsync();
        if (string.IsNullOrWhiteSpace(profile.Region) || string.IsNullOrWhiteSpace(profile.School) || string.IsNullOrWhiteSpace(profile.Team))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "PROFILE_INCOMPLETE" });

        var subjectIds = await db.Subjects.AsNoTracking()
            .Where(s => s.BookId == bookNumber)
            .Select(s => s.SubjectId)
            .ToListAsync();

        var studyWords = await db.Words.AsNoTracking()
            .Where(w => subjectIds.Contains(w.SubjectId) && w.LanguageId == studyLanguageId)
            .ToListAsync();

        if (studyWords.Count == 0)
            return new List<ExamWordItem>();

        var svgIds = studyWords.Select(w => w.SvgImageId).ToList();
        var translations = await db.Words.AsNoTracking()
            .Where(w => svgIds.Contains(w.SvgImageId) && w.LanguageId == transLanguageId)
            .ToDictionaryAsync(w => w.SvgImageId, w => w.WordName);

        var picked = studyWords
            .Where(w => translations.ContainsKey(w.SvgImageId))
            .OrderBy(_ => Random.Shared.Next())
            .Take(count)
            .ToList();

        // Words that mean the same ("bye" / "goodbye" -> "до свидания") are right answers for each other.
        var pickedMeanings = picked.Select(w => Jumbo.Shared.WordMatch.Normalize(translations[w.SvgImageId])).Distinct().ToList();
        var sameMeaning = (await db.Words.AsNoTracking()
            .Where(w => w.LanguageId == transLanguageId && pickedMeanings.Contains(w.WordName.ToLower()))
            .Select(w => new { w.SvgImageId, w.WordName })
            .ToListAsync())
            .GroupBy(x => Jumbo.Shared.WordMatch.Normalize(x.WordName))
            .ToDictionary(g => g.Key, g => g.Select(x => x.SvgImageId).Distinct().ToList());
        var meaningSvgIds = sameMeaning.Values.SelectMany(x => x).Distinct().ToList();
        var namesBySvg = (await db.Words.AsNoTracking()
            .Where(w => w.LanguageId == studyLanguageId && meaningSvgIds.Contains(w.SvgImageId))
            .Select(w => new { w.SvgImageId, w.WordName })
            .ToListAsync())
            .GroupBy(x => x.SvgImageId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.WordName).ToList());

        return picked
            .Select(w => new ExamWordItem
            {
                WordId = w.WordId,
                SvgImageId = w.SvgImageId,
                StudyWord = w.WordName,
                TranslationWord = translations[w.SvgImageId],
                AlternativeWords = sameMeaning.TryGetValue(Jumbo.Shared.WordMatch.Normalize(translations[w.SvgImageId]), out var svgs)
                    ? svgs.SelectMany(id => namesBySvg.GetValueOrDefault(id) ?? [])
                        .Where(n => !string.Equals(n, w.WordName, StringComparison.OrdinalIgnoreCase)).Distinct().ToList()
                    : new List<string>()
            })
            .ToList();
    }

    [HttpGet("test-level")]
    public async Task<ActionResult<List<ExamWordItem>>> GetTestLevelWords(
        [FromQuery] int studyLanguageId, [FromQuery] int transLanguageId, [FromQuery] int count)
    {
        var studyWords = await db.Words.AsNoTracking()
            .Where(w => w.LanguageId == studyLanguageId)
            .ToListAsync();

        if (studyWords.Count == 0)
            return new List<ExamWordItem>();

        var svgIds = studyWords.Select(w => w.SvgImageId).ToList();
        var translations = await db.Words.AsNoTracking()
            .Where(w => svgIds.Contains(w.SvgImageId) && w.LanguageId == transLanguageId)
            .ToDictionaryAsync(w => w.SvgImageId, w => w.WordName);

        return studyWords
            .Where(w => translations.ContainsKey(w.SvgImageId))
            .Select(w => new ExamWordItem
            {
                WordId = w.WordId,
                SvgImageId = w.SvgImageId,
                StudyWord = w.WordName,
                TranslationWord = translations[w.SvgImageId]
            })
            .OrderBy(_ => Random.Shared.Next())
            .Take(count)
            .ToList();
    }

    [HttpGet("alphabet-progress")]
    [RequireAccessToken]
    public async Task<ActionResult<List<LetterItem>>> GetAlphabetProgress([FromQuery] int studyLanguageId)
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;

        var allWords = await db.Words.AsNoTracking()
            .Where(w => w.LanguageId == studyLanguageId)
            .ToListAsync();

        var repLookup = await db.UsersWords.AsNoTracking()
            .Where(uw => uw.UserId == userId)
            .ToDictionaryAsync(uw => uw.WordId, uw => uw.RepeatingCount);

        var list = new List<LetterItem>();
        foreach (var ch in Alphabet)
        {
            var letter = ch.ToString();
            var letterWords = allWords.Where(w => !string.IsNullOrEmpty(w.WordName) && w.WordName[0].ToString().ToUpper() == letter).ToList();
            var total = letterWords.Count;

            int level;
            if (total == 0 || !letterWords.Any(w => repLookup.ContainsKey(w.WordId)))
            {
                level = -1;
            }
            else
            {
                var minRepeat = letterWords.Min(w => repLookup.TryGetValue(w.WordId, out var cnt) ? cnt : 0);
                level = minRepeat >= 3 ? 3 : minRepeat >= 2 ? 2 : minRepeat >= 1 ? 1 : 0;
            }

            var encounteredCount = total == 0 ? 0 : letterWords.Count(w => repLookup.ContainsKey(w.WordId));

            list.Add(new LetterItem
            {
                Letter = letter,
                TotalCount = total,
                LearnedCount = encounteredCount,
                RepetitionLevel = level
            });
        }

        return list;
    }
}
