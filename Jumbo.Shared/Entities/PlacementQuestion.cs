namespace Jumbo.Shared.Entities;

// The questions of the level test ("Test my level"). They live in the database (the first set is loaded from the file
// SeedData/Placement/questions.json when a question with that Code does not exist yet; after that the database is the source of truth).
// The picture questions ("what is this?") are not here: they are built from the words of the books.

// A text for reading questions (several questions point to one text).
public class PlacementPassage
{
    public int PlacementPassageId { get; set; }

    // A stable name ("r-a2-01"): lets the loader tell that a passage is already there.
    public string Code { get; set; } = null!;

    // A1 | A2 | B1 | B2
    public string Level { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Text { get; set; } = null!;
}

public class PlacementQuestion
{
    public int PlacementQuestionId { get; set; }

    public string Code { get; set; } = null!;

    // vocabulary | grammar | reading | listening
    public string Skill { get; set; } = null!;

    // A1 | A2 | B1 | B2
    public string Level { get; set; } = null!;

    // The question (grammar: the sentence with the gap).
    public string Prompt { get; set; } = null!;

    // The answers as written in the question; they are shuffled when a test is served.
    public List<string> Options { get; set; } = [];

    // The index of the right answer in Options.
    public int Answer { get; set; }

    // Reading questions: the text they are about.
    public int? PlacementPassageId { get; set; }

    // Listening questions: the lines the browser says aloud.
    public List<string> Audio { get; set; } = [];

    // A question that is switched off is never served (for one that turned out to be unclear or wrong).
    public bool IsActive { get; set; } = true;
}
