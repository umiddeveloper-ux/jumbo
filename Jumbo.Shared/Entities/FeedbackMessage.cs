namespace Jumbo.Shared.Entities;

/// <summary>A message written in the "Feedback" form in the footer of the website (with the address to answer to).</summary>
public class FeedbackMessage
{
    public int FeedbackMessageId { get; set; }
    public string Email { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string Lang { get; set; } = "ru";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
