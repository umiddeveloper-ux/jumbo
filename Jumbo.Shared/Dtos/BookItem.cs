namespace Jumbo.Shared.Dtos;

public class BookItem
{
    public int Number { get; set; }
    public double Progress { get; set; }  // 0.0 → 1.0
    public bool IsLocked { get; set; }

    public bool IsCompleted => Progress >= 1.0;
    public bool IsUnlocked => !IsLocked;
    public double ItemOpacity => IsLocked ? 0.4 : 1.0;
}
