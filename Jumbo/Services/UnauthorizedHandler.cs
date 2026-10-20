using System.Net;

namespace Jumbo.Services;

/// <summary>
/// The server says 401 to a request that carried the learner's token: the token is not valid any more (the account was deleted, the
/// login was reset...). The saved session is dropped and the learner is taken to the registration / login page with a short message.
/// </summary>
public class UnauthorizedHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var sentToken = request.Headers.Contains("X-Access-Token");
        var response = await base.SendAsync(request, cancellationToken);
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        bool authCall = path.Contains("/users/login", StringComparison.OrdinalIgnoreCase) || path.Contains("/users/register", StringComparison.OrdinalIgnoreCase)
                        || path.Contains("/auth/", StringComparison.OrdinalIgnoreCase);
        if (response.StatusCode == HttpStatusCode.Unauthorized && sentToken && !authCall)
            SessionExpired.Raise();
        return response;
    }
}

public static class SessionExpired
{
    private static int _busy;

    public static void Raise()
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1) return;
        UserSession.AccessToken = null;
        UserSession.UserId = 0;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                if (Shell.Current is not Shell shell) return;
                await shell.GoToAsync("//RegistrationPage");
                await shell.DisplayAlertAsync(LocalizationManager.Get("Error_Title"), LocalizationManager.Get("Session_Expired"), "OK");
            }
            catch { /* nothing more to do */ }
            finally { Interlocked.Exchange(ref _busy, 0); }
        });
    }
}
