namespace Jumbo.Shared;

/// <summary>
/// The printed cards come in boxes of 60 words, which are 4 topics of a book (a book of 300 words has 20 topics = 5 boxes). Box 1 holds
/// topics 1-4, box 2 topics 5-8 and so on. The QR code of a box opens the list of its 4 topics, the QR code of a book the list of all of them.
/// </summary>
public static class CardBoxes
{
    public const int TopicsPerBox = 4;

    /// <summary>How many boxes a book with this many topics has (the last box may hold fewer topics).</summary>
    public static int Count(int topicCount) => topicCount <= 0 ? 0 : (topicCount + TopicsPerBox - 1) / TopicsPerBox;

    /// <summary>The numbers of the first and the last topic of a box, or null when the book has no such box.</summary>
    public static (int First, int Last)? Range(int box, int topicCount)
    {
        if (box < 1 || box > Count(topicCount)) return null;
        var first = (box - 1) * TopicsPerBox + 1;
        return (first, Math.Min(box * TopicsPerBox, topicCount));
    }

    /// <summary>The box a topic (by its number in the book) belongs to.</summary>
    public static int BoxOf(int topicNumber) => (Math.Max(topicNumber, 1) - 1) / TopicsPerBox + 1;
}
