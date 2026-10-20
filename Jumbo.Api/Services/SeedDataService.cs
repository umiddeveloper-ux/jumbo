using Jumbo.Api.Data;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Jumbo.Api.Services;

public class SeedDataService(AppDbContext db, IHostEnvironment env, ILogger<SeedDataService> logger)
{
    private const string AwardsFile = "awards.json";
    private const string MarketFile = "market.json";

    // Файлы книг называются как book_01_a1_1_300.json, book_02_a1_301_600.json и т.д.
    private static readonly Regex BookFileRegex = new(@"^book_(\d+)_.*\.json$", RegexOptions.IgnoreCase);

    private string SeedDataDirectory => Path.Combine(env.ContentRootPath, "SeedData");
    private string BooksDirectory => Path.Combine(SeedDataDirectory, "Books");

    public async Task EnsureLanguagesSeededAsync()
    {
        if (!await db.Languages.AnyAsync())
        {
            db.Languages.AddRange(
                new Language { Name = "English", ShorName = "en" },
                new Language { Name = "Русский", ShorName = "ru" },
                new Language { Name = "O`zbekcha", ShorName = "uz" });
            await db.SaveChangesAsync();
        }
    }

    public async Task LoadSeedDataAsync()
    {
        await EnsureLanguagesSeededAsync();

        var (enId, ruId, uzId) = await GetLanguageIdsAsync();

        await SeedAwardsAsync(enId, ruId, uzId);
        await SeedMarketAsync(enId, ruId, uzId);
        await UpgradeMarketAsync(enId, ruId, uzId);

        // Все найденные книги сидируются сразу при старте API.
        // Доступность книги для пользователя всё равно регулируется прогрессом (см. UserSubjectProgress),
        // а не наличием данных в базе — так что ранняя загрузка не открывает книги досрочно.
        foreach (var bookNumber in DiscoverBookNumbers())
            await LoadBookDataAsync(bookNumber);

        await BackfillWordNumbersAsync();
        await BackfillExamplesAsync();
    }

    // Books seeded before example sentences existed: copies the examples from the book JSON into the rows that still have none.
    // A row is only touched when its word is exactly the JSON word, so a book whose content was replaced is left alone (use
    // ReloadBookAsync for that).
    private async Task BackfillExamplesAsync()
    {
        foreach (var bookNumber in DiscoverBookNumbers())
        {
            var filePath = FindBookFilePath(bookNumber);
            if (filePath is null) continue;
            var words = (await ReadJsonAsync<BookFileJson>(filePath))?.words;
            if (words is null || !words.Any(w => !string.IsNullOrWhiteSpace(w.example_en))) continue;

            var rows = await db.Words
                .Where(w => w.Example == null && db.Subjects.Any(s => s.SubjectId == w.SubjectId && s.BookId == bookNumber))
                .ToListAsync();
            if (rows.Count == 0) continue;

            var (enId, ruId, uzId) = await GetLanguageIdsAsync();
            var byNumber = rows.GroupBy(r => r.WordNumber).ToDictionary(g => g.Key, g => g.ToList());
            var changed = 0;
            foreach (var jw in words)
            {
                if (!byNumber.TryGetValue(jw.id, out var group)) continue;
                foreach (var row in group)
                {
                    var (text, example) = row.LanguageId == enId ? (jw.en, jw.example_en)
                        : row.LanguageId == ruId ? (jw.ru, jw.example_ru)
                        : row.LanguageId == uzId ? (jw.uz, jw.example_uz)
                        : ((string?)null, (string?)null);
                    if (text is null || Clean(example) is not { } ex || row.WordName != text) continue;
                    row.Example = ex;
                    changed++;
                }
            }

            if (changed > 0)
            {
                await db.SaveChangesAsync();
                logger.LogInformation("Backfilled {Count} example sentences for book {BookNumber}", changed, bookNumber);
            }
        }
    }

    // For a new edition of a book: reloads the books listed in the server setting SeedData__ReloadBooks (e.g. "1,2,3"), but
    // only those whose content in the database really differs from the file, so leaving the setting on cannot wipe learner
    // progress again on the next restart.
    public async Task ReloadChangedBooksAsync(IEnumerable<int> bookNumbers)
    {
        foreach (var bookNumber in bookNumbers.Distinct())
        {
            var filePath = FindBookFilePath(bookNumber);
            var words = filePath is null ? null : (await ReadJsonAsync<BookFileJson>(filePath))?.words;
            if (words is null || words.Count == 0)
            {
                logger.LogWarning("Book {BookNumber}: no file to reload from", bookNumber);
                continue;
            }

            var (enId, _, _) = await GetLanguageIdsAsync();
            var inDb = await db.Words
                .Where(w => w.LanguageId == enId && db.Subjects.Any(s => s.SubjectId == w.SubjectId && s.BookId == bookNumber))
                .Select(w => new { w.WordNumber, w.WordName })
                .ToListAsync();
            var same = inDb.Count == words.Count
                && words.All(jw => inDb.Any(d => d.WordNumber == jw.id && d.WordName == jw.en));
            if (same)
            {
                logger.LogInformation("Book {BookNumber} already matches its file: not reloaded", bookNumber);
                continue;
            }

            await ReloadBookAsync(bookNumber);
        }
    }

    // Deletes a book from the database: its topics, words and pictures, and the learners' progress in it. The exam results
    // that were stored for the reports stay (they only remember the book number).
    private async Task DeleteBookRowsAsync(int bookNumber)
    {
        var subjectIds = await db.Subjects.Where(s => s.BookId == bookNumber).Select(s => s.SubjectId).ToListAsync();
        var wordIds = db.Words.Where(w => subjectIds.Contains(w.SubjectId)).Select(w => w.WordId);
        var svgIds = await db.Words.Where(w => subjectIds.Contains(w.SubjectId)).Select(w => w.SvgImageId)
            .Concat(db.Subjects.Where(s => s.BookId == bookNumber).Select(s => s.SvgImageId))
            .Distinct().ToListAsync();

        await db.UsersWords.Where(u => wordIds.Contains(u.WordId)).ExecuteDeleteAsync();
        await db.UserDailyWords.Where(u => wordIds.Contains(u.WordId)).ExecuteDeleteAsync();
        await db.UserSubjectProgresses.Where(p => subjectIds.Contains(p.SubjectId)).ExecuteDeleteAsync();
        await db.Words.Where(w => subjectIds.Contains(w.SubjectId)).ExecuteDeleteAsync();
        await db.Subjects.Where(s => s.BookId == bookNumber).ExecuteDeleteAsync();
        await db.SvgImages.Where(i => svgIds.Contains(i.SvgImageId)).ExecuteDeleteAsync();
    }

    // Removes books that are no longer wanted (server setting SeedData__RemoveBooks="11,12,13"; the admin endpoint does the same
    // for one book). A book whose JSON file is still in SeedData would be seeded again at the next start: delete the file too.
    public async Task<int> RemoveBooksAsync(IEnumerable<int> bookNumbers)
    {
        var removed = 0;
        foreach (var bookNumber in bookNumbers.Distinct())
        {
            if (!await db.Subjects.AnyAsync(s => s.BookId == bookNumber)) continue;
            await using var tx = await db.Database.BeginTransactionAsync();
            await DeleteBookRowsAsync(bookNumber);
            await tx.CommitAsync();
            removed++;
            logger.LogWarning("Book {BookNumber} removed from the database", bookNumber);
            if (FindBookFilePath(bookNumber) is not null)
                logger.LogWarning("Book {BookNumber} still has a file in SeedData: it will be seeded again at the next start", bookNumber);
        }
        return removed;
    }

    // Replaces the content of one book with its JSON file (topics, words, pictures, examples). DESTRUCTIVE for the learners:
    // their progress in this book (learned words, topic results) is deleted together with the old words, because it points at
    // rows that no longer exist. Used when a book file was rewritten (new edition).
    public async Task<(int Subjects, int Words)> ReloadBookAsync(int bookNumber)
    {
        var filePath = FindBookFilePath(bookNumber)
            ?? throw new InvalidOperationException($"No SeedData file for book {bookNumber}.");
        var words = (await ReadJsonAsync<BookFileJson>(filePath))?.words;
        if (words is null || words.Count == 0)
            throw new InvalidOperationException($"The file of book {bookNumber} has no words.");

        await using var tx = await db.Database.BeginTransactionAsync();
        await DeleteBookRowsAsync(bookNumber);
        await LoadBookDataAsync(bookNumber);
        await tx.CommitAsync();

        var subjectCount = await db.Subjects.CountAsync(s => s.BookId == bookNumber);
        logger.LogWarning("Book {BookNumber} reloaded from its file: {Subjects} topics, {Words} words", bookNumber, subjectCount, words.Count);
        return (subjectCount, words.Count);
    }

    // Books seeded before WordNumber/Level existed have those columns at their default
    // (0 / null). Fixes them up from the same JSON files without re-inserting anything,
    // by zipping DB rows against JSON entries in the exact order LoadBookDataAsync created
    // them (Subjects ordered by SequenceNumber, Words ordered by WordId, 3 rows per JSON word).
    private async Task BackfillWordNumbersAsync()
    {
        if (!await db.Words.AnyAsync(w => w.WordNumber == 0)) return;

        foreach (var bookNumber in DiscoverBookNumbers())
            await BackfillBookWordNumbersAsync(bookNumber);
    }

    private async Task BackfillBookWordNumbersAsync(int bookNumber)
    {
        var filePath = FindBookFilePath(bookNumber);
        if (filePath is null) return;

        var bookFile = await ReadJsonAsync<BookFileJson>(filePath);
        var booksJson = bookFile?.words;
        if (booksJson is null || booksJson.Count == 0) return;

        var subjects = await db.Subjects.Where(s => s.BookId == bookNumber)
            .OrderBy(s => s.SequenceNumber).ToListAsync();
        var subjectGroups = booksJson.GroupBy(x => x.subject).ToList();

        if (subjects.Count != subjectGroups.Count)
        {
            logger.LogWarning(
                "Word-number backfill skipped for book {BookNumber}: subject count mismatch ({DbCount} in DB vs {JsonCount} in JSON)",
                bookNumber, subjects.Count, subjectGroups.Count);
            return;
        }

        bool anyChange = false;
        for (int i = 0; i < subjects.Count; i++)
        {
            var jsonWords = subjectGroups[i].ToList();
            var dbWords = await db.Words.Where(w => w.SubjectId == subjects[i].SubjectId)
                .OrderBy(w => w.WordId).ToListAsync();

            if (dbWords.Count != jsonWords.Count * 3)
            {
                logger.LogWarning(
                    "Word-number backfill skipped for subject {SubjectId}: word count mismatch ({DbCount} in DB vs {ExpectedCount} expected)",
                    subjects[i].SubjectId, dbWords.Count, jsonWords.Count * 3);
                continue;
            }

            for (int j = 0; j < jsonWords.Count; j++)
            {
                var jw = jsonWords[j];
                for (int lang = 0; lang < 3; lang++)
                {
                    var word = dbWords[j * 3 + lang];
                    if (word.WordNumber == 0) { word.WordNumber = jw.id; anyChange = true; }
                    if (string.IsNullOrEmpty(word.Level)) { word.Level = jw.level; anyChange = true; }
                }
            }
        }

        if (anyChange)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Backfilled word numbers/levels for book {BookNumber}", bookNumber);
        }
    }

    private List<int> DiscoverBookNumbers()
    {
        if (!Directory.Exists(BooksDirectory)) return [];

        var numbers = new List<int>();
        foreach (var file in Directory.GetFiles(BooksDirectory, "*.json"))
        {
            var match = BookFileRegex.Match(Path.GetFileName(file));
            if (match.Success)
                numbers.Add(int.Parse(match.Groups[1].Value));
        }

        numbers.Sort();
        return numbers;
    }

    private string? FindBookFilePath(int bookNumber)
    {
        if (!Directory.Exists(BooksDirectory)) return null;

        foreach (var file in Directory.GetFiles(BooksDirectory, "*.json"))
        {
            var match = BookFileRegex.Match(Path.GetFileName(file));
            if (match.Success && int.Parse(match.Groups[1].Value) == bookNumber)
                return file;
        }

        return null;
    }

    private async Task<(int enId, int ruId, int uzId)> GetLanguageIdsAsync()
    {
        var enId = await db.Languages.Where(l => l.ShorName == "en").Select(l => l.LanguageId).SingleAsync();
        var ruId = await db.Languages.Where(l => l.ShorName == "ru").Select(l => l.LanguageId).SingleAsync();
        var uzId = await db.Languages.Where(l => l.ShorName == "uz").Select(l => l.LanguageId).SingleAsync();
        return (enId, ruId, uzId);
    }

    private async Task SeedAwardsAsync(int enId, int ruId, int uzId)
    {
        if (await db.Awards.AnyAsync()) return;

        var awardsJson = await ReadJsonAsync<List<AwardsJson>>(AwardsFile);
        if (awardsJson is null || awardsJson.Count == 0) return;

        var newAwardIds = new List<int>();
        foreach (var award in awardsJson)
        {
            var svg = new SvgImage { SVG = award.svgImage };
            db.SvgImages.Add(svg);
            await db.SaveChangesAsync();

            var enAward = new Award { LanguageId = enId, SvgImageId = svg.SvgImageId, AwardName = award.enName };
            db.Awards.AddRange(
                enAward,
                new Award { LanguageId = ruId, SvgImageId = svg.SvgImageId, AwardName = award.ruName },
                new Award { LanguageId = uzId, SvgImageId = svg.SvgImageId, AwardName = award.uzName });
            await db.SaveChangesAsync();

            newAwardIds.Add(enAward.AwardId);
        }

        logger.LogInformation("Seeded {Count} awards", awardsJson.Count);
    }

    private async Task SeedMarketAsync(int enId, int ruId, int uzId)
    {
        if (await db.Markets.AnyAsync()) return;

        var marketJson = await ReadJsonAsync<List<MarketJson>>(MarketFile);
        if (marketJson is null || marketJson.Count == 0) return;

        foreach (var m in marketJson)
        {
            var svg = new SvgImage { SVG = m.svgImage };
            db.SvgImages.Add(svg);
            await db.SaveChangesAsync();

            db.Markets.AddRange(
                new Market { LanguageId = enId, SvgImageId = svg.SvgImageId, Name = m.enName, Price = m.price },
                new Market { LanguageId = ruId, SvgImageId = svg.SvgImageId, Name = m.ruName, Price = m.price },
                new Market { LanguageId = uzId, SvgImageId = svg.SvgImageId, Name = m.uzName, Price = m.price });
        }
        await db.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} market items", marketJson.Count);
    }

    // What each market item is and costs. The items were seeded with a price of 1000-20000 LP and did nothing; a learner earns about 20 LP
    // for a topic (a book is ~400), so the prices are brought down to that, and every item gets a kind and a key (what it does).
    // Found by the English name, so it also works on a database that already has the old items; it runs on every start and changes only
    // the rows that are not done yet (Kind empty).
    private static readonly (string En, string Kind, string Key, int Price)[] MarketCatalog =
    [
        ("One Extra Heart", "life", "life", 50),
        ("Full Hearts Refill", "refill", "refill", 150),
        ("Unicorn Shield Sound", "shield", "shield", 80),
        ("Red Paws Avatar", "avatar", "avatar_paws", 200),
        ("Bronze Frame", "frame", "frame_bronze", 300),
        ("Magic Wand Sound FX", "hidden", "sound_wand", 300),
        ("Smart Owl Avatar", "avatar", "avatar_owl", 400),
        ("Green Mint Theme", "theme", "theme_mint", 600),
        ("Silver Frame", "frame", "frame_silver", 600),
        ("Cute Robot Assistant", "pet", "pet_robot", 700),
        ("Space Theme", "theme", "theme_space", 900),
        ("Golden Frame", "frame", "frame_gold", 1000),
        ("Gold Lion Badge", "badge", "badge_lion", 1000),
        ("Super Cat Companion", "pet", "pet_cat", 1000),
        ("Candy Shop Theme", "theme", "theme_candy", 1200),
        ("Cyberpunk Neon Theme", "theme", "theme_neon", 1500),
        ("Fire Dragon Avatar", "avatar", "avatar_dragon", 1500),
        ("Diamond Frame", "frame", "frame_diamond", 2000),
        ("Cosmic Crown", "badge", "badge_crown", 2500),
        ("Legendary Rainbow Theme", "theme", "theme_rainbow", 2500),
    ];

    private async Task UpgradeMarketAsync(int enId, int ruId, int uzId)
    {
        var rows = await db.Markets.ToListAsync();
        if (rows.Count == 0) return;
        bool changed = false;

        foreach (var (en, kind, key, price) in MarketCatalog)
        {
            var enRow = rows.FirstOrDefault(r => r.LanguageId == enId && r.Name == en);
            if (enRow is null) continue;
            foreach (var r in rows.Where(r => r.SvgImageId == enRow.SvgImageId && r.Kind == string.Empty))
            {
                r.Kind = kind; r.Key = key; r.Price = price;
                changed = true;
            }
        }

        // the shield is not a sound: it takes the next mistake
        foreach (var r in rows.Where(r => r.Key == "shield" && !r.Name.Contains("hield") && !r.Name.Contains("Щит") && !r.Name.Contains("himoya")))
        {
            r.Name = r.LanguageId == ruId ? "Щит от ошибки" : r.LanguageId == uzId ? "Xatodan himoya" : "Mistake Shield";
            changed = true;
        }

        // new: the freeze of a streak
        if (!rows.Any(r => r.Key == "streak_freeze"))
        {
            var svg = new SvgImage { SVG = "<svg viewBox='0 0 24 24' xmlns='http://www.w3.org/2000/svg'><circle cx='12' cy='12' r='11' fill='#DDF4FF'/><g stroke='#1CB0F6' stroke-width='1.8' stroke-linecap='round'><path d='M12 3v18M4.2 7.5l15.6 9M4.2 16.5l15.6-9'/><path d='M12 3l-2 2M12 3l2 2M12 21l-2-2M12 21l2-2M4.2 7.5l2.7.4M4.2 7.5l1 2.5M19.8 16.5l-2.7-.4M19.8 16.5l-1-2.5M4.2 16.5l2.7-.4M4.2 16.5l1-2.5M19.8 7.5l-2.7.4M19.8 7.5l-1 2.5'/></g></svg>" };
            db.SvgImages.Add(svg);
            await db.SaveChangesAsync();
            db.Markets.AddRange(
                new Market { LanguageId = enId, SvgImageId = svg.SvgImageId, Name = "Streak Freeze", Price = 100, Kind = "freeze", Key = "streak_freeze" },
                new Market { LanguageId = ruId, SvgImageId = svg.SvgImageId, Name = "Заморозка серии", Price = 100, Kind = "freeze", Key = "streak_freeze" },
                new Market { LanguageId = uzId, SvgImageId = svg.SvgImageId, Name = "Seriyani muzlatish", Price = 100, Kind = "freeze", Key = "streak_freeze" });
            changed = true;
        }

        if (changed) await db.SaveChangesAsync();
    }

    public async Task LoadBookDataAsync(int bookNumber)
    {
        var hasSubjects = await db.Subjects.AnyAsync(s => s.BookId == bookNumber);
        if (hasSubjects) return;

        var filePath = FindBookFilePath(bookNumber);
        if (filePath is null)
        {
            logger.LogWarning("Book file not found for book {BookNumber}", bookNumber);
            return;
        }

        var bookFile = await ReadJsonAsync<BookFileJson>(filePath);
        var booksJson = bookFile?.words;
        if (booksJson is null || booksJson.Count == 0) return;

        var (enId, ruId, uzId) = await GetLanguageIdsAsync();

        int sequence = 1;
        foreach (var group in booksJson.GroupBy(x => x.subject))
        {
            var wordsInSubject = group.ToList();

            // SVG первого слова используется как иконка субъекта
            var subjectSvg = new SvgImage { SVG = wordsInSubject[0].svg };
            db.SvgImages.Add(subjectSvg);
            await db.SaveChangesAsync();

            var subject = new Subject
            {
                BookId = bookNumber,
                IsPassed = false,
                SequenceNumber = sequence,
                SubjectName = wordsInSubject[0].subject,
                SvgImageId = subjectSvg.SvgImageId
            };
            db.Subjects.Add(subject);
            await db.SaveChangesAsync();

            foreach (var w in wordsInSubject)
            {
                var svg = new SvgImage { SVG = w.svg };
                db.SvgImages.Add(svg);
                await db.SaveChangesAsync();

                db.Words.AddRange(WordRows(w, subject.SubjectId, svg.SvgImageId, enId, ruId, uzId));
            }
            await db.SaveChangesAsync();

            sequence++;
        }

        logger.LogInformation("Seeded book {BookNumber} with {Count} subjects", bookNumber, sequence - 1);
    }

    // Чинит повреждённые данные: если у субъекта нет слов — загружает их из JSON
    public async Task FixSubjectWordsAsync(int subjectId)
    {
        var subject = await db.Subjects.FindAsync(subjectId);
        if (subject is null) return;

        var hasWords = await db.Words.AnyAsync(w => w.SubjectId == subjectId);
        if (hasWords) return;

        var filePath = FindBookFilePath(subject.BookId);
        if (filePath is null) return;

        var bookFile = await ReadJsonAsync<BookFileJson>(filePath);
        var booksJson = bookFile?.words;
        if (booksJson is null) return;

        var subjectGroup = booksJson.Where(x => x.subject == subject.SubjectName).ToList();
        if (subjectGroup.Count == 0) return;

        var (enId, ruId, uzId) = await GetLanguageIdsAsync();

        bool isFirstWord = true;
        foreach (var w in subjectGroup)
        {
            var svg = new SvgImage { SVG = w.svg };
            db.SvgImages.Add(svg);
            await db.SaveChangesAsync();

            if (isFirstWord && subject.SvgImageId == 0)
            {
                subject.SvgImageId = svg.SvgImageId;
                isFirstWord = false;
            }

            db.Words.AddRange(WordRows(w, subjectId, svg.SvgImageId, enId, ruId, uzId));
        }
        await db.SaveChangesAsync();
    }

    // The three rows (English, Russian, Uzbek) of one book word. Each row carries the example sentence in its own language.
    private static Word[] WordRows(BookJson w, int subjectId, int svgImageId, int enId, int ruId, int uzId) =>
    [
        new Word { LanguageId = enId, SubjectId = subjectId, SvgImageId = svgImageId, WordName = w.en, FirstSymbol = FirstSymbolOf(w.en), WordNumber = w.id, Level = w.level, Example = Clean(w.example_en) },
        new Word { LanguageId = ruId, SubjectId = subjectId, SvgImageId = svgImageId, WordName = w.ru, FirstSymbol = FirstSymbolOf(w.ru), WordNumber = w.id, Level = w.level, Example = Clean(w.example_ru) },
        new Word { LanguageId = uzId, SubjectId = subjectId, SvgImageId = svgImageId, WordName = w.uz, FirstSymbol = FirstSymbolOf(w.uz), WordNumber = w.id, Level = w.level, Example = Clean(w.example_uz) },
    ];

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string FirstSymbolOf(string word) =>
        string.IsNullOrEmpty(word) ? string.Empty : word[0].ToString().ToUpperInvariant();

    private async Task<T?> ReadJsonAsync<T>(string fileName, string? directory = null)
    {
        var path = Path.Combine(directory ?? SeedDataDirectory, fileName);
        if (!File.Exists(path))
        {
            logger.LogWarning("Seed file not found: {Path}", path);
            return default;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error deserializing seed file {Path}", path);
            return default;
        }
    }

    private class AwardsJson
    {
        public string enName { get; set; } = string.Empty;
        public string ruName { get; set; } = string.Empty;
        public string uzName { get; set; } = string.Empty;
        public string svgImage { get; set; } = string.Empty;
    }

    private class MarketJson
    {
        public string enName { get; set; } = string.Empty;
        public string ruName { get; set; } = string.Empty;
        public string uzName { get; set; } = string.Empty;
        public string svgImage { get; set; } = string.Empty;
        public int price { get; set; }
    }

    private class BookFileJson
    {
        public List<BookJson> words { get; set; } = [];
    }

    private class BookJson
    {
        public int id { get; set; }
        public string level { get; set; } = string.Empty;
        public string subject { get; set; } = string.Empty;
        public string en { get; set; } = string.Empty;
        public string ru { get; set; } = string.Empty;
        public string uz { get; set; } = string.Empty;
        public string svg { get; set; } = string.Empty;

        // Example sentence of the word in each language (optional: older books have none).
        public string? example_en { get; set; }
        public string? example_ru { get; set; }
        public string? example_uz { get; set; }
    }
}
