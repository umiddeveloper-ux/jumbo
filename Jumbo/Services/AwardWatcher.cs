using Jumbo.ApiClient;
using Jumbo.Pages;
using Jumbo.Shared.Dtos;

namespace Jumbo.Services;

/// <summary>
/// "You got an award!": asks the server for the awards the learner has earned since the last time (each is told once) and shows a window
/// with an animation for every one, with the buttons "Awards" and "Close". It is called after a page is opened and when the LP grow
/// (a won game), so the window appears right after the game that earned the award.
/// </summary>
public class AwardWatcher(LpApiClient apiClient, LivesService lives)
{
    private CancellationTokenSource? _debounce;
    private bool _busy;

    /// <summary>Check a little later (several triggers in a row make one request).</summary>
    public void CheckSoon(int delayMs = 900)
    {
        _debounce?.Cancel();
        _debounce = new CancellationTokenSource();
        var token = _debounce.Token;
        _ = Task.Delay(delayMs, token).ContinueWith(t =>
        {
            if (!t.IsCanceled) MainThread.BeginInvokeOnMainThread(async () => await CheckAsync());
        });
    }

    public async Task CheckAsync()
    {
        if (_busy || !UserSession.IsRegistered) return;
        _busy = true;
        try
        {
            List<AwardItem> fresh;
            try { fresh = await apiClient.GetNewAwardsAsync(UserLanguagePrefs.TransLangId); }
            catch { return; }                                  // the window is a bonus: never break the app

            if (fresh.Count > 0) await lives.InitAsync();      // the awards paid LP: read the numbers again (a stale copy must not overwrite them)

            foreach (var award in fresh)
            {
                var nav = Shell.Current?.Navigation;
                if (nav is null) return;
                var page = new AwardPopupPage(award);
                await nav.PushModalAsync(page, animated: false);
                bool openAwards = await page.Result;
                await nav.PopModalAsync(animated: false);
                if (openAwards)
                {
                    await Shell.Current!.GoToAsync("//awards");
                    return;
                }
            }
        }
        finally { _busy = false; }
    }
}
