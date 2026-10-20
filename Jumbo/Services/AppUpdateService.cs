using Jumbo.ApiClient;
using Jumbo.Pages;
using Jumbo.Shared.Dtos;

namespace Jumbo.Services;

/// <summary>
/// "A new version is out". The app asks the server (the table of versions in the database, edited in the admin page) at its start and now and
/// then: when its own build number is lower than the newest one it offers "Update" (and asks again after a day, not at every start); when it
/// is lower than the minimum it shows a screen that cannot be skipped. "Update" opens the link from the database (the store page).
/// </summary>
public class AppUpdateService(LpApiClient api)
{
    private DateTime _lastCheck = DateTime.MinValue;
    private bool _showing;

    private static string? PlatformKey =>
        DeviceInfo.Platform == DevicePlatform.Android ? "android" :
        DeviceInfo.Platform == DevicePlatform.iOS ? "ios" :
        DeviceInfo.Platform == DevicePlatform.WinUI ? "windows" : null;

    /// <summary>The number of this build (the version code: ApplicationVersion in the project).</summary>
    public static int MyCode => int.TryParse(AppInfo.Current.BuildString, out var n) ? n : 0;

    public async Task CheckAsync()
    {
        if (_showing || PlatformKey is not { } platform) return;
        if (DateTime.UtcNow - _lastCheck < TimeSpan.FromHours(3)) return;
        _lastCheck = DateTime.UtcNow;

        AppVersionDto? v;
        try { v = await api.GetAppVersionAsync(platform); }
        catch { _lastCheck = DateTime.MinValue; return; }              // no network: try again at the next page
        if (v is null) return;

        int mine = MyCode;
        string notes = LocalizationService.CurrentCode switch { "en" => v.NotesEn, "uz" => v.NotesUz, _ => v.NotesRu } ?? string.Empty;
        string text = string.IsNullOrWhiteSpace(notes) ? LocalizationManager.Get("Update_Text") : notes;

        if (v.MinCode > mine)
        {
            _showing = true;
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var page = new UpdateRequiredPage(LocalizationManager.Get("Update_Required_Title"),
                    string.IsNullOrWhiteSpace(notes) ? LocalizationManager.Get("Update_Required_Text") : notes,
                    LocalizationManager.Get("Update_Now"), () => OpenAsync(v.Url));
                if (Shell.Current?.Navigation is { } nav) await nav.PushModalAsync(page, false);
            });
            return;
        }

        if (v.LatestCode > mine)
        {
            // asked again after a day, for each newer version separately
            string key = $"update-asked-{v.LatestCode}";
            var last = Preferences.Default.Get(key, DateTime.MinValue.ToBinary());
            if (DateTime.UtcNow - DateTime.FromBinary(last) < TimeSpan.FromHours(24)) return;
            Preferences.Default.Set(key, DateTime.UtcNow.ToBinary());

            _showing = true;
            try
            {
                bool update = await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlertAsync(
                    "🎉 " + LocalizationManager.Get("Update_Title"), text,
                    LocalizationManager.Get("Update_Now"), LocalizationManager.Get("Update_Later")));
                if (update) await OpenAsync(v.Url);
            }
            catch { /* the page changed while the question was open */ }
            finally { _showing = false; }
        }
    }

    private static async Task OpenAsync(string? url)
    {
        var target = string.IsNullOrWhiteSpace(url) ? "https://jumbo.uz/" : url.Trim();
        if (target.StartsWith('/')) target = ApiConfig.BaseUrl.TrimEnd('/') + target;
        try { await Launcher.Default.OpenAsync(new Uri(target)); }
        catch { try { await Launcher.Default.OpenAsync(new Uri("https://jumbo.uz/")); } catch { } }
    }
}
