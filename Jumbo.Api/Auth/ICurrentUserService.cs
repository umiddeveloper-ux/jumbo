namespace Jumbo.Api.Auth;

public interface ICurrentUserService
{
    Task<int?> GetUserIdAsync();
}
