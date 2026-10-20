namespace Jumbo.Shared.Dtos;

public class DeleteAccountRequest
{
    // Proves the caller knows the password, not just that they hold a (long-lived) access token.
    public string? CurrentPassword { get; set; }
}
