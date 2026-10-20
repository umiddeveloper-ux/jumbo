namespace Jumbo.ApiClient;

/// <summary>Too many wrong passwords in a row: the account cannot be logged in to for a few minutes.</summary>
public class LoginBlockedException(int minutes) : Exception("TOO_MANY")
{
    public int Minutes { get; } = minutes;
}
