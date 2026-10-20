namespace Jumbo.Shared.Dtos;

/// <summary>The footer form of the website: an address to answer to, a message (may be empty) and "send me the news" (default yes).</summary>
public class SubscribeRequest
{
    public string? Email { get; set; }
    public string? Lang { get; set; }
    public string? Message { get; set; }
    public bool? Subscribe { get; set; }
}

public class SubscriberDto
{
    public string Email { get; set; } = string.Empty;
    public string Lang { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class FeedbackDto
{
    public string Email { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Lang { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
