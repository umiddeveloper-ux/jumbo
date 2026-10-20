using Microsoft.AspNetCore.Mvc.Filters;

namespace Jumbo.Api.Auth;

public class RequireAccessTokenAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();
        var userId = await currentUser.GetUserIdAsync();

        if (userId is null)
        {
            context.Result = new Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult(new
            {
                error = "Missing or invalid X-Access-Token header."
            });
            return;
        }

        await next();
    }
}
