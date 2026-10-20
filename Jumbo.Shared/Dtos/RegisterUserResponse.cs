namespace Jumbo.Shared.Dtos;

public class RegisterUserResponse
{
    public int UserId { get; set; }
    public string AccessToken { get; set; } = null!;
}
