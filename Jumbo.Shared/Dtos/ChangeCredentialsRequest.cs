namespace Jumbo.Shared.Dtos;

public class ChangeCredentialsRequest
{
    // Required for accounts that already have a password; proves the caller knows it,
    // not just that they hold a (long-lived) access token.
    public string? CurrentPassword { get; set; }

    // Leave empty to keep the current login / password.
    public string? NewUsername { get; set; }
    public string? NewPassword { get; set; }
}
