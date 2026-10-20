namespace Jumbo.Shared.Dtos;

public class QrSessionResponse
{
    public Guid SessionId { get; set; }
    public DateTime ExpiresAt { get; set; }
}
