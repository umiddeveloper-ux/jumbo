using System.ComponentModel;
using Jumbo.Resources.Strings;

namespace Jumbo.Services;

/// <summary>
/// Live string lookup for XAML: <c>{u:Tr Key=Home_Continue}</c> binds to the indexer, so a language change
/// (see <see cref="LocalizationService.SetLanguage"/>) refreshes every visible text without reopening pages.
/// The strings come from the same resx files as the typed <see cref="AppResources"/> class (kept in sync with the
/// website's strings by tools/sync-mobile-strings.mjs).
/// </summary>
public sealed class LocalizationManager : INotifyPropertyChanged
{
    public static LocalizationManager Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => Get(key);

    public static string Get(string key) =>
        AppResources.ResourceManager.GetString(key, AppResources.Culture) ?? key;

    public static string Format(string key, params object[] args) => string.Format(Get(key), args);

    // An empty property name means "everything changed": MAUI re-reads every [key] indexer binding
    // (naming "Item[]" is not enough on Android, so most captions kept the old language).
    public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}
