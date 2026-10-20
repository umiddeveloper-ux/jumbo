using Jumbo.ApiClient;
using Jumbo.Services;
using Jumbo.Shared.Dtos;

namespace Jumbo.Pages;

public partial class LoginPage : ContentPage
{
    private readonly LpApiClient _apiClient;
    private bool _busy;

    public LoginPage(LpApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = !string.IsNullOrEmpty(message);
    }

    private async void OnPasswordCompleted(object? sender, EventArgs e) => await LoginAsync();

    private async void OnLoginClicked(object? sender, EventArgs e) => await LoginAsync();

    private async Task LoginAsync()
    {
        if (_busy) return;
        ShowError(string.Empty);

        if (string.IsNullOrWhiteSpace(UsernameEntry.Text) || string.IsNullOrWhiteSpace(PasswordEntry.Text))
        {
            ShowError(LocalizationManager.Get("Common_EnterCredentials"));
            return;
        }

        _busy = true;
        LoginButton.IsEnabled = false;
        try
        {
            RegisterUserResponse? response;
            try
            {
                response = await _apiClient.LoginAsync(new LoginRequest
                {
                    Username = UsernameEntry.Text.Trim(),
                    Password = PasswordEntry.Text
                });
            }
            catch (LoginBlockedException ex)
            {
                ShowError(LocalizationManager.Format("Login_TooMany", ex.Minutes));
                return;
            }
            catch (Exception ex)
            {
                ShowError(LocalizationManager.Get("Common_ErrorPrefix") + ex.Message);
                return;
            }

            if (response is null)
            {
                ShowError(LocalizationManager.Get("Login_InvalidCredentials"));
                return;
            }

            UserSession.UserId = response.UserId;
            UserSession.AccessToken = response.AccessToken;

            // The account may have been created on another device (web or a different
            // phone), so its saved language prefs must be fetched — there's no picker here.
            var user = await _apiClient.GetCurrentUserAsync();
            if (user is not null)
            {
                Preferences.Default.Set("user_firstname", user.FirstName);
                UserLanguagePrefs.Save(user.AppLanguageId, user.StudyLanguageId, user.TranslationLanguageId);

                var languages = await _apiClient.GetLanguagesAsync();
                var appLang = languages.FirstOrDefault(l => l.LanguageId == user.AppLanguageId);
                if (appLang is not null)
                    LocalizationService.SetLanguage(appLang.ShorName);
            }

            await Shell.Current.GoToAsync("//main");
        }
        finally
        {
            _busy = false;
            LoginButton.IsEnabled = true;
        }
    }

    private async void OnGoToRegisterTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("//RegistrationPage");
    }
}
