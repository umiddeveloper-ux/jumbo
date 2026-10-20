namespace Jumbo.Shared.Entities;

public class UsersWord
{
    public int UsersWordId { get; set; }

    public int UserId { get; set; }

    public int WordId { get; set; }

    public int LessonCount { get; set; }

    public int RepeatingCount { get; set; }
}
