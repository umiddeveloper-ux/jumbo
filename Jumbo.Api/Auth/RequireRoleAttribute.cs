using Jumbo.Api.Data;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Auth;

// Access-token check plus a role check: 401 without a valid token, 403 when the account's role is not allowed.
// The role is read from the database on every call, so a revoked role takes effect immediately.
public class RequireRoleAttribute(params UserRole[] roles) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var userId = await services.GetRequiredService<ICurrentUserService>().GetUserIdAsync();
        if (userId is null)
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Missing or invalid X-Access-Token header." });
            return;
        }

        var db = services.GetRequiredService<AppDbContext>();
        var role = await db.Users.AsNoTracking().Where(u => u.UserId == userId).Select(u => u.Role).SingleAsync();
        if (Array.IndexOf(roles, role) < 0)
        {
            context.Result = new ObjectResult(new { error = "FORBIDDEN_ROLE" }) { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        await next();
    }
}
