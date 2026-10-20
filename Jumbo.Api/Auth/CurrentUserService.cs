using Jumbo.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Auth;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor, AppDbContext db) : ICurrentUserService
{
    public const string HeaderName = "X-Access-Token";

    private int? _cachedUserId;
    private bool _resolved;

    public async Task<int?> GetUserIdAsync()
    {
        if (_resolved) return _cachedUserId;
        _resolved = true;

        var token = httpContextAccessor.HttpContext?.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token)) return null;

        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.AccessToken == token);
        _cachedUserId = user?.UserId;
        return _cachedUserId;
    }
}
