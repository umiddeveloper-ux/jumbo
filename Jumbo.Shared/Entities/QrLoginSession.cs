namespace Jumbo.Shared.Entities;

public class QrLoginSession
{
    public Guid SessionId { get; set; }

    public string Status { get; set; } = "Pending";

    public int? UserId { get; set; }

    public string? AccessToken { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }
}
