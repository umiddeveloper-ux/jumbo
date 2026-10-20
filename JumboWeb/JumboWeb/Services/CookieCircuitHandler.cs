using Microsoft.AspNetCore.Components.Server.Circuits;

namespace JumboWeb.Services;

// Captures the "access_token" cookie into the circuit's SessionTokenAccessor when the
// circuit is opened. OnCircuitOpenedAsync still runs while the connecting request's
// HttpContext is available via IHttpContextAccessor — unlike component code, which runs
// entirely inside the SignalR circuit with no HttpContext at all (true regardless of
// whether prerendering is enabled).
public class CookieCircuitHandler(IHttpContextAccessor httpContextAccessor, SessionTokenAccessor tokenAccessor) : CircuitHandler
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null && httpContext.Request.Cookies.TryGetValue("access_token", out var token))
            tokenAccessor.Token = token;

        return base.OnCircuitOpenedAsync(circuit, cancellationToken);
    }
}
