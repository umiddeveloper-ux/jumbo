namespace Jumbo.Shared;

/// <summary>
/// The learner's CEFR level ("A1", "A2", ...). It follows the book the learner is working on:
/// books 1-3 teach the A1 words, 4-6 A2, 7-10 B1, 11 and above B2 (same split as the printed books).
/// </summary>
public static class LanguageLevels
{
    public static string ForBook(int bookNumber) => bookNumber switch
    {
        <= 3 => "A1",
        <= 6 => "A2",
        <= 10 => "B1",
        _ => "B2"
    };
}
