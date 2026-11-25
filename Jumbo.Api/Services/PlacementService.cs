using System.Text.RegularExpressions;
using Jumbo.Api.Data;
using Jumbo.Shared;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Services;

// Builds one block of the level test: vocabulary questions (pictures of the words of the books where the level has books, otherwise
// text questions), and grammar, reading and listening questions from the database (table placement_questions). Options are shuffled here,
// so the right answer is not always in the same place.
public class PlacementService(AppDbContext db)
{
    private const int VocabularyCount = 4;
    private const int GrammarCount = 5;
    private const int ListeningCount = 4;      // and one reading text of three questions: 16 questions in a block

    // The books of a level (the course: 1-3 A1, 4-6 A2, 7-10 B1). B2 has no books: its vocabulary questions are text questions.
    private static readonly Dictionary<string, int[]> BooksOf = new() { ["A1"] = [1, 2, 3], ["A2"] = [4, 5, 6], ["B1"] = [7, 8, 9, 10] };

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
                  "Art & Drawing", "Camping & Outdoors", "Garden & Plants", "Natural Disasters", "Pets & Animals", "Containers & Portions"],
        ["B1"] = ["Groceries", "Home Appliances", "Repairs at Home", "Technology", "Fashion", "Personal Care", "Public Transport", "City Services",
                  "Crafts & DIY", "Inventions", "Safety at Home", "Weather 2", "Health & Fitness", "Symptoms & Treatment", "Accidents & Injuries",
                  "Road Rules", "Healthy Eating"]
    };

    private static readonly Regex PlainWord = new("^[A-Za-z]{3,12}$", RegexOptions.Compiled);

    // Words of the concrete topics that a picture still does not show clearly (adjectives, abstract nouns, verbs): never used.
    // Add a word here when a learner or a teacher reports a picture question that is not clear.
    private static readonly HashSet<string> Unclear = new(StringComparer.OrdinalIgnoreCase)
    {
        "bitter", "state", "contract", "download", "story", "loud", "online", "search", "comment", "deadline", "notification", "contact",
        "times", "red", "burn", "dive", "evacuate", "exhibition", "stage", "rock", "bin", "app", "trail", "leather", "hobby", "department"
    };

    public static bool IsLevel(string level) => PlacementScoring.Levels.Contains(level);

    public async Task<PlacementBlockDto> BuildBlockAsync(string level)
    {
        var rnd = Random.Shared;
        var questions = new List<PlacementQuestionDto>();

        questions.AddRange(await VocabularyAsync(level, VocabularyCount));

        var active = db.PlacementQuestions.AsNoTracking().Where(q => q.IsActive && q.Level == level);

        questions.AddRange((await active.Where(q => q.Skill == PlacementSkill.Grammar).ToListAsync())
            .OrderBy(_ => rnd.Next()).Take(GrammarCount)
            .Select(q => Dto(q, PlacementSkill.Grammar)));

        // one text with its questions (a text with no question left is skipped)
        var texts = await db.PlacementPassages.AsNoTracking().Where(p => p.Level == level).ToListAsync();
        var readQuestions = await active.Where(q => q.Skill == PlacementSkill.Reading && q.PlacementPassageId != null).ToListAsync();
        var text = texts.Where(t => readQuestions.Any(q => q.PlacementPassageId == t.PlacementPassageId)).OrderBy(_ => rnd.Next()).FirstOrDefault();
        if (text is not null)
        {
            questions.AddRange(readQuestions.Where(q => q.PlacementPassageId == text.PlacementPassageId).OrderBy(q => q.PlacementQuestionId).Take(3)
                .Select(q =>
                {
                    var d = Dto(q, PlacementSkill.Reading);
                    d.PassageTitle = text.Title;
                    d.Passage = text.Text;
                    return d;
                }));
        }

        var recorded = (await db.PlacementAudios.AsNoTracking().Select(a => a.PlacementQuestionId).ToListAsync()).ToHashSet();
        questions.AddRange((await active.Where(q => q.Skill == PlacementSkill.Listening).ToListAsync())
            .OrderBy(_ => rnd.Next()).Take(ListeningCount)
            .Select(q =>
            {
                var d = Dto(q, PlacementSkill.Listening);
                d.Audio = q.Audio;
                d.AudioId = recorded.Contains(q.PlacementQuestionId) ? q.PlacementQuestionId : null;
                return d;
            }));

        return new PlacementBlockDto { Level = level, Questions = questions };
    }

    private static PlacementQuestionDto Dto(Jumbo.Shared.Entities.PlacementQuestion q, string skill) =>
        Shuffled(new PlacementQuestionDto { Id = q.Code, Skill = skill, Level = q.Level, Prompt = q.Prompt }, q.Options, q.Answer);

    // Vocabulary: "What is this?" - a picture of a word of the level and four English words (the others come from other topics, so none
    // of them can also fit the picture) - for the levels that have books; what is missing is filled with text questions from the database.
    private async Task<List<PlacementQuestionDto>> VocabularyAsync(string level, int count)
    {
        var result = BooksOf.ContainsKey(level) ? await PictureQuestionsAsync(level, count) : [];

        if (result.Count < count)
        {
            var text = await db.PlacementQuestions.AsNoTracking()
                .Where(q => q.IsActive && q.Level == level && q.Skill == PlacementSkill.Vocabulary).ToListAsync();
            result.AddRange(text.OrderBy(_ => Random.Shared.Next()).Take(count - result.Count).Select(q => Dto(q, PlacementSkill.Vocabulary)));
        }
        return result;
    }

    private async Task<List<PlacementQuestionDto>> PictureQuestionsAsync(string level, int count)
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
        foreach (var target in words.OrderBy(_ => rnd.Next()))
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
}
