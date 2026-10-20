namespace Jumbo.Shared.Entities;

public class UserSubjectProgress
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int SubjectId { get; set; }
    public bool FindMeWon { get; set; }
    public bool SequenceWon { get; set; }
    public bool ListenWon { get; set; }
    public bool StepsWon { get; set; }

    // "Build the phrase" game.
    public bool PhraseWon { get; set; }

    // A topic has three games that count: Find me, Listen and find, Build the phrase (the ring of the topic has three parts, one per
    // game). Sequence and Steps are extra games. Before the Phrase game and this rule existed a topic was passed with the four games
    // Find / Sequence / Listen / Steps: such a topic keeps its third part, so nobody loses a topic they have passed.
    public const int Parts = 3;

    public bool PhraseCounted => PhraseWon || (SequenceWon && StepsWon);

    /// <summary>How many of the three parts of the topic's ring are filled (0..3).</summary>
    public int PartsDone => (FindMeWon ? 1 : 0) + (ListenWon ? 1 : 0) + (PhraseCounted ? 1 : 0);

    public bool AllWon => PartsDone == Parts;
}
