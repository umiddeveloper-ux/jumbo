using System.Text.Json;
using Jumbo.Api.Data;
using Jumbo.Shared;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Services;

// Puts the first set of level test questions (the file Data/PlacementQuestions.json, built into the API) into the database.
// Only what is missing is added, by Code: a question that is already in the database is never overwritten (the database is the source of
// truth: a question edited or switched off there stays so), and a new question added to the file arrives with the next start.
public class PlacementSeeder(AppDbContext db, ILogger<PlacementSeeder> log)
{
    public async Task SeedAsync()
    {
        using var stream = typeof(PlacementSeeder).Assembly.GetManifestResourceStream("placement-questions.json")
                           ?? throw new InvalidOperationException("The level test questions are missing from the API.");
        var bank = await JsonSerializer.DeserializeAsync<Bank>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? throw new InvalidOperationException("The level test questions could not be read.");

        var haveQuestions = (await db.PlacementQuestions.Select(q => q.Code).ToListAsync()).ToHashSet();
        var passages = await db.PlacementPassages.ToDictionaryAsync(p => p.Code);
        var added = 0;

        foreach (var g in bank.Grammar.Where(g => !haveQuestions.Contains(g.Id)))
        {
            db.PlacementQuestions.Add(new PlacementQuestion { Code = g.Id, Skill = PlacementSkill.Grammar, Level = g.Level, Prompt = g.Q, Options = g.Options, Answer = g.Answer });
            added++;
        }
        foreach (var v in bank.Vocabulary.Where(v => !haveQuestions.Contains(v.Id)))
        {
            db.PlacementQuestions.Add(new PlacementQuestion { Code = v.Id, Skill = PlacementSkill.Vocabulary, Level = v.Level, Prompt = v.Q, Options = v.Options, Answer = v.Answer });
            added++;
        }
        foreach (var l in bank.Listening.Where(l => !haveQuestions.Contains(l.Id)))
        {
            db.PlacementQuestions.Add(new PlacementQuestion { Code = l.Id, Skill = PlacementSkill.Listening, Level = l.Level, Prompt = l.Q, Options = l.Options, Answer = l.Answer, Audio = l.Audio });
            added++;
        }
        await db.SaveChangesAsync();

        foreach (var r in bank.Reading)
        {
            if (!passages.TryGetValue(r.Id, out var passage))
            {
                passage = new PlacementPassage { Code = r.Id, Level = r.Level, Title = r.Title, Text = r.Text };
                db.PlacementPassages.Add(passage);
                await db.SaveChangesAsync();                    // the id of the text is needed by its questions
            }
            for (var i = 0; i < r.Questions.Count; i++)
            {
                var code = $"{r.Id}-{i + 1}";
                if (haveQuestions.Contains(code)) continue;
                var q = r.Questions[i];
                db.PlacementQuestions.Add(new PlacementQuestion
                {
                    Code = code, Skill = PlacementSkill.Reading, Level = r.Level, Prompt = q.Q, Options = q.Options, Answer = q.Answer, PlacementPassageId = passage.PlacementPassageId
                });
                added++;
            }
        }
        await db.SaveChangesAsync();

        if (added > 0) log.LogInformation("Level test: {Count} questions added to the database", added);
    }

    private class Bank
    {
        public List<Item> Grammar { get; set; } = [];
        public List<Item> Vocabulary { get; set; } = [];
        public List<ListeningItem> Listening { get; set; } = [];
        public List<ReadingItem> Reading { get; set; } = [];
    }

    private class Item
    {
        public string Id { get; set; } = "";
        public string Level { get; set; } = "";
        public string Q { get; set; } = "";
        public List<string> Options { get; set; } = [];
        public int Answer { get; set; }
    }

    private class ListeningItem : Item
    {
        public List<string> Audio { get; set; } = [];
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
}
