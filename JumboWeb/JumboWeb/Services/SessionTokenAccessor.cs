namespace JumboWeb.Services;

// Scoped holder for the current circuit's access token. Populated once per circuit
// by middleware (from the "access_token" cookie) while a real HttpContext is still
// available — Razor component code running inside the SignalR circuit has no HttpContext.
public class SessionTokenAccessor
{
    public string? Token { get; set; }
}
