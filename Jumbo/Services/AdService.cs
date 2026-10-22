using Jumbo.ApiClient;
using Jumbo.Pages;

namespace Jumbo.Services;

/// <summary>
/// The ads: asks the server "is there an ad for me now?" a little after a page is opened and every few minutes, and shows it in a window.
/// The server answers with an ad only for a registered learner of the free version when the time of an ad has come. Nothing is shown in the
/// middle of a game, an exam or a test (only on the main pages), not in the first seconds after the start (the app opens the road of the
/// book by itself then) and not while a page is changing; the server is asked at most once in two minutes.
/// </summary>
public class AdService(LpApiClient api)
{
    private static readonly TimeSpan Every = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan MinAsk = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan SettleAfterStart = TimeSpan.FromSeconds(12);
    private static readonly DateTime Started = DateTime.UtcNow;

    private DateTime _lastAsk = DateTime.MinValue;
    private bool _showing;
    private CancellationTokenSource? _debounce;
    private IDispatcherTimer? _timer;

    /// <summary>Check a little later (several triggers in a row make one request).</summary>
    public void CheckSoon(int delayMs = 2500)
    {
        _debounce?.Cancel();
        _debounce = new CancellationTokenSource();
        var token = _debounce.Token;
        _ = Task.Delay(delayMs, token).ContinueWith(t =>
        {
            if (!t.IsCanceled) MainThread.BeginInvokeOnMainThread(async () => await CheckAsync());
        });
    }

    // the pages where an ad may appear: the tabs, the road of a book and the page of a topic (not a game)
    private static bool OnAMainPage() =>
        Shell.Current?.CurrentPage is MainPage or DictionaryPage or AwardsPage or GamesPage or SettingsPage or BookPage or SubjectDetailPage;

    // true when the learner is on a main page and it has not changed for a moment (the app is not in the middle of opening another page)
    private static async Task<bool> WaitUntilCalmAsync(int seconds)
    {
        for (int i = 0; i < seconds; i++)
        {
            var page = Shell.Current?.CurrentPage;
            if (OnAMainPage())
            {
                await Task.Delay(1200);
                if (ReferenceEquals(page, Shell.Current?.CurrentPage) && OnAMainPage()) return true;
            }
            else await Task.Delay(1000);
        }
        return false;
    }

    public async Task CheckAsync()
    {
        EnsureTimer();
        if (_showing || !UserSession.IsRegistered) return;

        var sinceStart = DateTime.UtcNow - Started;
        if (sinceStart < SettleAfterStart) { CheckSoon((int)(SettleAfterStart - sinceStart).TotalMilliseconds + 500); return; }
        if (DateTime.UtcNow - _lastAsk < MinAsk) return;
        if (!OnAMainPage()) return;                              // in a game: the next check

        _showing = true;                                         // (also keeps a second check away while we wait)
        try
        {
            if (!await WaitUntilCalmAsync(6)) return;
            _lastAsk = DateTime.UtcNow;

            Jumbo.Shared.Dtos.AdForUserDto? ad;
            try { ad = await api.GetNextAdAsync(); }
            catch { return; }                                    // an ad is never worth breaking the app
            if (ad is null) return;

            // the ad is told to the server as seen: it must really be shown, so wait again if the page changed while we asked
            if (!OnAMainPage() && !await WaitUntilCalmAsync(8)) return;

            var nav = Shell.Current?.Navigation;
            if (nav is null) return;
            var page = new AdPage(ad);
            await nav.PushModalAsync(page, animated: false);
            await page.Result;
            await nav.PopModalAsync(animated: false);
        }
        catch { /* the page changed while the window was open */ }
        finally { _showing = false; }
    }

    private void EnsureTimer()
    {
        if (_timer is not null || Application.Current?.Dispatcher is not { } dispatcher) return;
        _timer = dispatcher.CreateTimer();
        _timer.Interval = Every;
        _timer.Tick += async (_, _) => await CheckAsync();
        _timer.Start();
    }
}
