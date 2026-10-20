using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jumbo.ApiClient;
using Jumbo.Services;

namespace Jumbo.PageModels;

public record LanguageOption(string Code, string Name);

/// <summary>The 3-step sign-up wizard (same flow as the website): about you -> login + password -> school and team (optional).</summary>
public partial class RegistrationPageModel : ObservableObject
{
    private const int TotalSteps = 3;
    private const int MinPasswordLength = 6;

    private readonly LpApiClient _api;

    public List<LanguageOption> Languages { get; } = [new("en", "English"), new("ru", "Русский"), new("uz", "O'zbekcha")];

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsStep1), nameof(IsStep2), nameof(IsStep3), nameof(CanGoBack), nameof(IsLastStep), nameof(Progress), nameof(StepTitle), nameof(StepOfText))]
    private int _step = 1;

    [ObservableProperty] private string _firstName = string.Empty;
    [ObservableProperty] private string _lastName = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;

    // Only a "+" and digits (at most 15), grouped by the country's pattern.
    partial void OnPhoneChanged(string value)
    {
        var masked = PhoneMask.Format(value);
        if (masked != value) Phone = masked;
    }
    [ObservableProperty] private LanguageOption? _studyLanguage;
    [ObservableProperty] private LanguageOption? _translationLanguage;

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;

    [ObservableProperty] private int _avatarIndex;      // the first preset by default

    [ObservableProperty] private string _region = string.Empty;
    [ObservableProperty] private int? _classId;

    [ObservableProperty] private string _error = string.Empty;
    [ObservableProperty] private bool _isBusy;

    /// <summary>Example number for the phone's country (+998 00 000 00 00 in Uzbekistan).</summary>
    public string PhoneHint { get; } = PhoneHints.Current;

    public bool IsStep1 => Step == 1;
    public bool IsStep2 => Step == 2;
    public bool IsStep3 => Step == 3;
    public bool CanGoBack => Step > 1;
    public bool IsLastStep => Step == TotalSteps;
    public double Progress => (double)Step / TotalSteps;
    public string StepTitle => LocalizationManager.Get($"Reg_Step{Step}Title");
    public string StepOfText => LocalizationManager.Format("Reg_StepOf", Step, TotalSteps);

    public RegistrationPageModel(LpApiClient api)
    {
        _api = api;
        StudyLanguage = Languages[0];
        TranslationLanguage = Languages[1];
#if DEBUG
        // Screenshots / manual checks: JUMBO_STEP=3 opens the wizard on that step.
        if (int.TryParse(Environment.GetEnvironmentVariable("JUMBO_STEP"), out var debugStep) && debugStep is >= 1 and <= TotalSteps) Step = debugStep;
#endif

        LocalizationManager.Instance.PropertyChanged += (_, _) => MainThread.BeginInvokeOnMainThread(() =>
        {
            OnPropertyChanged(nameof(StepTitle));
            OnPropertyChanged(nameof(StepOfText));
        });
    }

    [RelayCommand]
    private async Task Next()
    {
        if (IsBusy) return;
        Error = string.Empty;

        if (Step == 1)
        {
            if (string.IsNullOrWhiteSpace(FirstName)) { Error = LocalizationManager.Get("EnterFirstName"); return; }
            if (StudyLanguage is null || TranslationLanguage is null) { Error = LocalizationManager.Get("Reg_StudyLangHelp"); return; }
        }
        else if (Step == 2)
        {
            var login = Username.Trim();
            if (login.Length == 0 || string.IsNullOrEmpty(Password)) { Error = LocalizationManager.Get("Common_EnterCredentials"); return; }
            if (Password.Length < MinPasswordLength) { Error = LocalizationManager.Get("Reg_PasswordTooShort"); return; }
            if (Password != ConfirmPassword) { Error = LocalizationManager.Get("Reg_PasswordsMismatch"); return; }

            IsBusy = true;
            try
            {
                if (!await _api.IsUsernameAvailableAsync(login))
                {
                    Error = LocalizationManager.Get("Reg_UsernameTaken");
                    return;
                }
            }
            catch
            {
                // Offline hiccup: the server checks again on the final step.
            }
            finally { IsBusy = false; }
        }

        Step++;
    }

    [RelayCommand]
    private void Back()
    {
        Error = string.Empty;
        if (Step > 1) Step--;
    }

    [RelayCommand]
    private async Task Submit()
    {
        if (Step < TotalSteps) await Next();
        else await Finish(skipProfile: false);
    }

    [RelayCommand]
    private async Task Skip() => await Finish(skipProfile: true);

    private async Task Finish(bool skipProfile)
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = string.Empty;
        try
        {
            var appCode = LocalizationService.CurrentCode;
            var appLang = await _api.GetLanguageByShortNameAsync(appCode);
            var studyLang = await _api.GetLanguageByShortNameAsync(StudyLanguage!.Code);
            var transLang = await _api.GetLanguageByShortNameAsync(TranslationLanguage!.Code);

            var phone = Phone.Trim();
            _ = int.TryParse(new string(phone.Where(char.IsDigit).ToArray()), out int legacyPhone);

            RegisterUserResponse? response = await _api.RegisterAsync(new RegisterUserRequest
            {
                FirstName = FirstName.Trim(),
                LastName = LastName.Trim(),
                PhoneNumber = legacyPhone,
                Phone = string.IsNullOrEmpty(phone) ? null : phone,
                AppLanguageId = appLang?.LanguageId ?? 0,
                StudyLanguageId = studyLang?.LanguageId ?? 0,
                TranslationLanguageId = transLang?.LanguageId ?? 0,
                TranslationLanguageId2 = null,
                Username = Username.Trim(),
                Password = Password,
                Region = skipProfile || string.IsNullOrEmpty(Region) ? null : Region,
                ClassId = skipProfile ? null : ClassId,
                AvatarSvg = Jumbo.Shared.PresetAvatars.All[Math.Clamp(AvatarIndex, 0, Jumbo.Shared.PresetAvatars.All.Count - 1)]
            });

            if (response is null)
            {
                Error = LocalizationManager.Get("Register_Failed");
                return;
            }

            UserSession.UserId = response.UserId;
            UserSession.AccessToken = response.AccessToken;
            Preferences.Default.Set("user_firstname", FirstName.Trim());

            LocalizationService.SetLanguage(appCode);
            UserLanguagePrefs.Save(appLang?.LanguageId ?? 0, studyLang?.LanguageId ?? 0, transLang?.LanguageId ?? 0);

            await Shell.Current.GoToAsync("//main");
        }
        catch (Exception ex)
        {
            // The login could have been taken between step 2 and now: send the user back to fix it.
            if (ex.Message.Contains("логин", StringComparison.OrdinalIgnoreCase) && ex.Message.Contains("занят", StringComparison.OrdinalIgnoreCase))
            {
                Step = 2;
                Error = LocalizationManager.Get("Reg_UsernameTaken");
            }
            else
            {
                Error = LocalizationManager.Get("Common_ErrorPrefix") + ex.Message;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GoToLogin() => await Shell.Current.GoToAsync("LoginPage");

    // The privacy policy lives on the website (Google Play and the privacy rules want it reachable from the app).
    [RelayCommand]
    private async Task OpenPrivacy()
    {
        try { await Launcher.Default.OpenAsync(new Uri($"{Jumbo.Services.ApiConfig.BaseUrl}privacy")); } catch { /* no browser: nothing to do */ }
    }
}
