using System.Text.Json;
using System.Text.RegularExpressions;
using Jumbo.Api.Data;
using Jumbo.Shared;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Services;

// Builds one block of the level test: a few vocabulary questions made from the words and pictures of the books, and grammar, reading
// and listening questions drawn from our own bank (SeedData/Placement/questions.json). Options are shuffled here, so the right answer is not
// always in the same place.
public class PlacementService(AppDbContext db, IHostEnvironment env)
{
    private const int VocabularyCount = 6;
    private const int GrammarCount = 6;
    private const int ListeningCount = 5;      // and one reading text of three questions: 20 questions in a block

    // The books of a level (the course: 1-3 A1, 4-6 A2, 7-10 B1).
    private static readonly Dictionary<string, int[]> BooksOf = new() { ["A1"] = [1, 2, 3], ["A2"] = [4, 5, 6] };

    // The topics of things you can see (the picture of the word is clearly "this word"): only they are used for the picture questions.
    // Grammar words, numbers, days, adjectives, verbs and feelings are left out: a picture of them can be read in more than one way.
    private static readonly Dictionary<string, string[]> ConcreteTopics = new()
    {
        ["A1"] = ["Family", "People", "Body", "Domestic Animals", "Colours", "Clothes", "Fruits", "Food", "Drinks", "Bedroom", "Bathroom", "Classroom",
                  "Sports", "Weather", "TV", "Jobs", "Kitchen", "Vegetables", "City", "Wild Animals", "Hobbies", "Restaurant", "Living Room",
                  "Clothes and Accessories", "Transportation", "School", "Insects", "Around the Home", "Music", "Home", "Nature", "Farm & Village",
                  "Sea Animals", "Cinema and Theatre", "Shopping"],
        ["A2"] = ["Computers", "Phone", "Airport", "Maths & Shapes", "Geography", "Space", "Birds", "Fishing & Boats", "Body 2",
                  "First Aid & Pharmacy", "Family 2", "Materials & Tools", "Food 2", "Office & Work", "Internet & Social Media", "Books & Reading",
                  "Art & Drawing", "Camping & Outdoors", "Garden & Plants", "Natural Disasters", "Pets & Animals", "Containers & Portions"]
    };

    private static readonly Regex PlainWord = new("^[A-Za-z]{3,12}$", RegexOptions.Compiled);

    // Words of the concrete topics that a picture still does not show clearly (adjectives, abstract nouns, verbs): never used.
    // Add a word here when a learner or a teacher reports a picture question that is not clear.
    private static readonly HashSet<string> Unclear = new(StringComparer.OrdinalIgnoreCase)
    {
        "bitter", "state", "contract", "download", "story", "loud", "online", "search", "comment", "deadline", "notification", "contact",
        "times", "red", "burn", "dive", "evacuate", "exhibition", "stage", "rock", "bin", "app", "trail", "leather", "hobby", "department"
    };

    private static Bank? _bank;
    private static readonly object BankLock = new();

    public static bool IsLevel(string level) => BooksOf.ContainsKey(level);

    public async Task<PlacementBlockDto> BuildBlockAsync(string level)
    {
        var bank = LoadBank();
        var rnd = Random.Shared;
        var questions = new List<PlacementQuestionDto>();

        questions.AddRange(await VocabularyAsync(level, VocabularyCount));

        questions.AddRange(bank.Grammar.Where(g => g.Level == level).OrderBy(_ => rnd.Next()).Take(GrammarCount)
            .Select(g => Shuffled(new PlacementQuestionDto { Id = g.Id, Skill = PlacementSkill.Grammar, Level = level, Prompt = g.Q }, g.Options, g.Answer)));

        var text = bank.Reading.Where(r => r.Level == level).OrderBy(_ => rnd.Next()).FirstOrDefault();
        if (text is not null)
            questions.AddRange(text.Questions.Select((q, i) => Shuffled(new PlacementQuestionDto
            {
                Id = $"{text.Id}-{i + 1}", Skill = PlacementSkill.Reading, Level = level, Prompt = q.Q, PassageTitle = text.Title, Passage = text.Text
            }, q.Options, q.Answer)));

        questions.AddRange(bank.Listening.Where(l => l.Level == level).OrderBy(_ => rnd.Next()).Take(ListeningCount)
            .Select(l => Shuffled(new PlacementQuestionDto { Id = l.Id, Skill = PlacementSkill.Listening, Level = level, Prompt = l.Q, Audio = l.Audio }, l.Options, l.Answer)));

        return new PlacementBlockDto { Level = level, Questions = questions };
    }

    // "What is this?": a picture of a word of the level and four English words (the others come from other topics, so none of them can
    // also fit the picture).
    private async Task<List<PlacementQuestionDto>> VocabularyAsync(string level, int count)
    {
        var books = BooksOf[level];
        var topics = await db.Subjects.AsNoTracking().Where(s => books.Contains(s.BookId))
            .Select(s => new { s.SubjectId, s.SubjectName }).ToListAsync();
        var allowed = ConcreteTopics[level];
        var clear = topics.Where(t => allowed.Contains(t.SubjectName.Trim(), StringComparer.OrdinalIgnoreCase)).Select(t => t.SubjectId).ToHashSet();
        var topicIds = clear.ToList();

        var words = (await db.Words.AsNoTracking().Where(w => w.LanguageId == 1 && topicIds.Contains(w.SubjectId))
                .Select(w => new { w.WordName, w.SvgImageId, w.SubjectId }).ToListAsync())
            .Where(w => PlainWord.IsMatch(w.WordName) && !Unclear.Contains(w.WordName)).ToList();

        var rnd = Random.Shared;
        var result = new List<PlacementQuestionDto>();
        var usedTopics = new HashSet<int>();
        foreach (var target in words.Where(w => clear.Contains(w.SubjectId)).OrderBy(_ => rnd.Next()))
        {
            if (result.Count >= count) break;
            if (!usedTopics.Add(target.SubjectId)) continue;        // one word from a topic: the questions are spread over the level

            var others = words.Where(w => w.SubjectId != target.SubjectId && w.SvgImageId != target.SvgImageId
                                          && !string.Equals(w.WordName, target.WordName, StringComparison.OrdinalIgnoreCase))
                .GroupBy(w => w.WordName.ToLowerInvariant()).Select(g => g.First())
                .OrderBy(_ => rnd.Next()).Take(3).Select(w => w.WordName.ToLowerInvariant()).ToList();
            if (others.Count < 3) continue;

            var options = new List<string> { target.WordName.ToLowerInvariant() };
            options.AddRange(others);
            result.Add(Shuffled(new PlacementQuestionDto
            {
                Id = $"v-{target.SvgImageId}", Skill = PlacementSkill.Vocabulary, Level = level, SvgImageId = target.SvgImageId
            }, options, 0));
        }
        return result;
    }

    private static PlacementQuestionDto Shuffled(PlacementQuestionDto q, List<string> options, int answer)
    {
        var order = Enumerable.Range(0, options.Count).OrderBy(_ => Random.Shared.Next()).ToList();
        q.Options = order.Select(i => options[i]).ToList();
        q.Answer = order.IndexOf(answer);
        return q;
    }

    private Bank LoadBank()
    {
        if (_bank is not null) return _bank;
        lock (BankLock)
        {
            if (_bank is not null) return _bank;
            var path = Path.Combine(env.ContentRootPath, "SeedData", "Placement", "questions.json");
            _bank = JsonSerializer.Deserialize<Bank>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new InvalidOperationException("The level test questions could not be read.");
            return _bank;
        }
    }

    private class Bank
    {
        public List<GrammarItem> Grammar { get; set; } = [];
        public List<ReadingItem> Reading { get; set; } = [];
        public List<ListeningItem> Listening { get; set; } = [];
    }

    private class GrammarItem
    {
        public string Id { get; set; } = "";
        public string Level { get; set; } = "";
        public string Q { get; set; } = "";
        public List<string> Options { get; set; } = [];
        public int Answer { get; set; }
    }

    private class ReadingItem
    {
        public string Id { get; set; } = "";
        public string Level { get; set; } = "";
        public string Title { get; set; } = "";
        public string Text { get; set; } = "";
        public List<ReadingQuestion> Questions { get; set; } = [];
    }

    private class ReadingQuestion
    {
        public string Q { get; set; } = "";
        public List<string> Options { get; set; } = [];
        public int Answer { get; set; }
    }

    private class ListeningItem
    {
        public string Id { get; set; } = "";
        public string Level { get; set; } = "";
        public List<string> Audio { get; set; } = [];
        public string Q { get; set; } = "";
        public List<string> Options { get; set; } = [];
        public int Answer { get; set; }
    }
}
