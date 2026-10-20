namespace Jumbo.Shared.Entities;

/// <summary>A visitor who left an e-mail in the footer of the website ("subscribe"): news about the platform. Nothing else is kept.</summary>
public class Subscriber
{
    public int SubscriberId { get; set; }

    // Lower case, unique.
    public string Email { get; set; } = null!;

    // The language of the page the address was left on: ru | en | uz.
    public string Lang { get; set; } = "ru";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
