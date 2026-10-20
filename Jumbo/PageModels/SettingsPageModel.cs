using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jumbo.ApiClient;
using Jumbo.Controls;
using Jumbo.Services;
using System.Collections.ObjectModel;

namespace Jumbo.PageModels;

public partial class SettingsPageModel : ObservableObject
{
    private readonly LpApiClient _apiClient;
    private readonly ModalErrorHandler _errorHandler;
    private readonly LivesService _livesService;
    private User? _currentUser;
    private bool _dataLoaded;
    private bool _loading;                  // while the form is filled from the server, changes are not "edits"

    [ObservableProperty]
    private bool _isBusy;

    public string PhoneHint { get; } = PhoneHints.Current;

    // ── profile ──
    [ObservableProperty]
    private string _firstName = string.Empty;

    [ObservableProperty]
    private string _lastName = string.Empty;

    [ObservableProperty]
    private string _phone = string.Empty;

    // Only a "+" and digits (at most 15), grouped by the country's pattern.
    partial void OnPhoneChanged(string value)
    {
        var masked = PhoneMask.Format(value);
        if (masked != value) Phone = masked;
    }

    // ── the ID of the learner (A0000001): read out and typed, used in payments ──
    [ObservableProperty]
    private string _publicId = string.Empty;

    [ObservableProperty]
    private string _balanceText = string.Empty;

    [RelayCommand]
    private async Task CopyId()
    {
        if (string.IsNullOrEmpty(PublicId)) return;
        try
        {
            await Clipboard.Default.SetTextAsync(PublicId);
            AvatarMessage = LocalizationManager.Get("Settings_IdCopied");
        }
        catch { /* no clipboard: the ID is on the screen */ }
    }

    // ── the version of the app (bronze / silver / gold) and the link to the page of the site ──
    [ObservableProperty]
    private string _planKey = Jumbo.Shared.Plans.Bronze;

    [ObservableProperty]
    private string _planTitle = string.Empty;

    [ObservableProperty]
    private string _planSub = string.Empty;

    private void ShowPlan()
    {
        var plan = _livesService.Plan;
        PlanKey = plan;
        PlanTitle = LocalizationManager.Format("Plan_Yours", LocalizationManager.Get(plan switch
        {
            Jumbo.Shared.Plans.Silver => "Plan_Silver",
            Jumbo.Shared.Plans.Gold => "Plan_Gold",
            _ => "Plan_Bronze"
        }));
        PlanSub = plan == Jumbo.Shared.Plans.Bronze || _livesService.PlanUntil is not { } until
            ? LocalizationManager.Get("Plan_FreeVersion")
            : LocalizationManager.Format("Plan_PaidUntil", until.ToLocalTime().ToString("dd.MM.yyyy"));
    }

    [RelayCommand]
    private async Task OpenPlans()
    {
        try { await Launcher.Default.OpenAsync(new Uri($"{ApiConfig.BaseUrl}plans")); } catch { /* no browser */ }
    }

    // ── avatar: the preset pictures; a choice is saved at once ──
    [ObservableProperty]
    private int _avatarIndex = -1;

    [ObservableProperty]
    private string _avatarMessage = string.Empty;

    partial void OnAvatarIndexChanged(int value)
    {
        if (_loading || value < 0 || value >= Jumbo.Shared.PresetAvatars.All.Count) return;
        _ = SaveAvatarAsync(Jumbo.Shared.PresetAvatars.All[value]);
    }

    private async Task SaveAvatarAsync(string svg)
    {
        try
        {
            AvatarMessage = string.Empty;
            var user = await _apiClient.SetAvatarAsync(svg);
            if (user is null) return;
            if (_currentUser is not null) { _currentUser.AvatarSvg = user.AvatarSvg; _currentUser.AvatarDefaultSvg = user.AvatarDefaultSvg; }
            _livesService.AvatarSvg = user.AvatarSvg;                // the title bar shows it at once
            AvatarMessage = LocalizationManager.Get("Settings_AvatarSaved");
        }
        catch (Exception ex) { _errorHandler.HandleError(ex); }
    }

    // ── account: login and password (the same card as on the website) ──
    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _currentPassword = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private string _accountError = string.Empty;

    [ObservableProperty]
    private string _accountMessage = string.Empty;

    [RelayCommand]
    private async Task SaveAccount()
    {
        AccountError = AccountMessage = string.Empty;
        if (_currentUser is null) return;

        var newUsername = Username.Trim();
        bool usernameChanged = newUsername != (_currentUser.Username ?? string.Empty);
        bool passwordChanged = !string.IsNullOrEmpty(NewPassword);

        if (string.IsNullOrEmpty(newUsername)) { AccountError = LocalizationManager.Get("Settings_UsernameRequired"); return; }
        if (!usernameChanged && !passwordChanged) { AccountError = LocalizationManager.Get("Settings_AccountNothingToChange"); return; }
        if (passwordChanged && NewPassword != ConfirmPassword) { AccountError = LocalizationManager.Get("Settings_PasswordsMismatch"); return; }

        try
        {
            IsBusy = true;
            await _apiClient.ChangeCredentialsAsync(new ChangeCredentialsRequest
            {
                CurrentPassword = CurrentPassword,
                NewUsername = usernameChanged ? newUsername : null,
                NewPassword = passwordChanged ? NewPassword : null
            });
            _currentUser.Username = newUsername;
            CurrentPassword = NewPassword = ConfirmPassword = string.Empty;
            AccountMessage = LocalizationManager.Get("Settings_AccountSaved");
        }
        catch (Exception ex) { AccountError = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task OpenPrivacy()
    {
        try { await Launcher.Default.OpenAsync(new Uri($"{ApiConfig.BaseUrl}privacy")); } catch { /* no browser */ }
    }

    // ── delete the account for good (Google Play wants it in the app) ──
    [ObservableProperty]
    private string _deletePassword = string.Empty;

    [ObservableProperty]
    private string _deleteError = string.Empty;

    [RelayCommand]
    private async Task DeleteAccount()
    {
        DeleteError = string.Empty;
        if (string.IsNullOrEmpty(DeletePassword)) { DeleteError = LocalizationManager.Get("Settings_DeleteNeedPassword"); return; }

        bool sure = await Shell.Current.DisplayAlertAsync(LocalizationManager.Get("Settings_DeleteConfirmTitle"), LocalizationManager.Get("Settings_DeleteHint"),
            LocalizationManager.Get("Settings_DeleteButton"), LocalizationManager.Get("Common_Cancel"));
        if (!sure) return;

        try
        {
            IsBusy = true;
            await _apiClient.DeleteAccountAsync(new DeleteAccountRequest { CurrentPassword = DeletePassword });
        }
        catch (Exception ex) { DeleteError = ex.Message; return; }
        finally { IsBusy = false; }

        // the account is gone: the same as a logout
        DeletePassword = string.Empty;
        UserSession.AccessToken = null;
        UserSession.UserId = 0;
        _dataLoaded = false;
        _currentUser = null;
        _livesService.Streak = 0;
        _livesService.AvatarSvg = null;
        _livesService.FirstName = string.Empty;
        await Shell.Current.GoToAsync("//RegistrationPage");
    }

    // ── school + team (learners) ──
    [ObservableProperty]
    private bool _isLearner = true;

    [ObservableProperty]
    private MyTeamDto? _myTeam;

    [ObservableProperty]
    private string _teamStatusText = string.Empty;

    [ObservableProperty]
    private string _teamStateText = string.Empty;

    [ObservableProperty]
    private bool _teamApproved;

    [ObservableProperty]
    private string _legacyText = string.Empty;

    [ObservableProperty]
    private string _pickedRegion = string.Empty;

    [ObservableProperty]
    private int? _pickedClassId;

    [ObservableProperty]
    private string _teamMessage = string.Empty;

    [ObservableProperty]
    private string _teamError = string.Empty;

    public bool HasTeam => MyTeam is not null;
    public bool HasNoTeam => MyTeam is null;
    public bool HasLegacy => !string.IsNullOrEmpty(LegacyText);
    public bool CanSendTeam => PickedClassId is not null && !IsBusy;

    // ── Telegram bot (learners) ──
    private TelegramStatusDto? _botStatus;

    [ObservableProperty]
    private bool _botAvailable;

    [ObservableProperty]
    private bool _botConnected;

    [ObservableProperty]
    private bool _botWaiting;

    [ObservableProperty]
    private bool _botConnectVisible;

    [ObservableProperty]
    private string _botStatusText = string.Empty;

    private async Task LoadBotAsync()
    {
        _botStatus = IsLearner ? await _apiClient.GetMyTelegramAsync() : null;
        BotAvailable = IsLearner && _botStatus is { Enabled: true };
        BotConnected = _botStatus?.Connections.Any(c => c.Connected) == true;
        BotWaiting = !BotConnected && _botStatus?.Connections.Any(c => !c.Connected) == true;
        BotConnectVisible = BotAvailable && !BotConnected;
        BotStatusText = BotConnected ? LocalizationManager.Get("PupilBot_Connected") : BotWaiting ? LocalizationManager.Get("PupilBot_Waiting") : string.Empty;
    }

    [RelayCommand]
    private async Task ConnectBot()
    {
        try
        {
            var link = await _apiClient.CreateMyTelegramLinkAsync();
            await LoadBotAsync();
            if (link?.Url is { Length: > 0 } url) await Launcher.Default.OpenAsync(new Uri(url));
        }
        catch (Exception ex) { _errorHandler.HandleError(ex); }
    }

    [RelayCommand]
    private async Task CheckBot()
    {
        try { await LoadBotAsync(); }
        catch (Exception ex) { _errorHandler.HandleError(ex); }
    }

    [RelayCommand]
    private async Task DisconnectBot()
    {
        try
        {
            foreach (var c in _botStatus?.Connections.Where(c => c.Connected).ToList() ?? [])
                await _apiClient.RemoveMyTelegramAsync(c.Id);
            await LoadBotAsync();
        }
        catch (Exception ex) { _errorHandler.HandleError(ex); }
    }

    // ── daily goal ──
    public List<ChipItem> GoalChips { get; } = [new("5", "5"), new("10", "10"), new("20", "20")];

    [ObservableProperty]
    private string _dailyGoalKey = "10";

    [ObservableProperty]
    private string _goalMessage = string.Empty;

    // ── privacy / behaviour ──
    [ObservableProperty]
    private bool _hideName;

    [ObservableProperty]
    private bool _hideTooltips;

    // ── appearance + language ──
    public List<ChipItem> ThemeChips { get; private set; } = [];
    public List<ChipItem> LanguageChips { get; } = LocalizationService.Languages.Select(l => new ChipItem(l.Code, l.Name)).ToList();

    [ObservableProperty]
    private string _themeKey = "light";

    [ObservableProperty]
    private string _appLanguageKey = "ru";

    public ObservableCollection<LanguageModel> AvailableLanguages { get; } = [];

    [ObservableProperty]
    private LanguageModel? _selectedTargetLanguage;

    [ObservableProperty]
    private LanguageModel? _selectedStudyLanguage;

    [ObservableProperty]
    private string _savedMessage = string.Empty;

    public SettingsPageModel(LpApiClient apiClient, ModalErrorHandler errorHandler, LivesService livesService)
    {
        _apiClient = apiClient;
        _errorHandler = errorHandler;
        _livesService = livesService;

        BuildThemeChips();
        LocalizationManager.Instance.PropertyChanged += (_, _) => MainThread.BeginInvokeOnMainThread(() =>
        {
            BuildThemeChips();
            RebuildTeamTexts();
            ShowPlan();
        });
        _livesService.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(LivesService.Plan)) MainThread.BeginInvokeOnMainThread(ShowPlan);
        };
    }

    private void BuildThemeChips()
    {
        ThemeChips =
        [
            new("light", "☀️ " + LocalizationManager.Get("Theme_Light")),
            new("dark", "🌙 " + LocalizationManager.Get("Theme_Dark")),
            new("system", "📱 " + LocalizationManager.Get("Theme_System"))
        ];
        OnPropertyChanged(nameof(ThemeChips));
    }

    partial void OnMyTeamChanged(MyTeamDto? value)
    {
        OnPropertyChanged(nameof(HasTeam));
        OnPropertyChanged(nameof(HasNoTeam));
        RebuildTeamTexts();
    }

    partial void OnPickedClassIdChanged(int? value) => OnPropertyChanged(nameof(CanSendTeam));

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanSendTeam));

    partial void OnLegacyTextChanged(string value) => OnPropertyChanged(nameof(HasLegacy));

    private void RebuildTeamTexts()
    {
        if (MyTeam is { } t)
        {
            TeamStatusText = (t.Approved ? "✅ " : "⏳ ") + LocalizationManager.Format("Team_Is", t.ClassName, t.SchoolName);
            TeamStateText = LocalizationManager.Get(t.Approved ? "Team_Approved" : "Team_Pending");
            TeamApproved = t.Approved;
        }
        else if (_currentUser is { } u && (!string.IsNullOrWhiteSpace(u.School) || !string.IsNullOrWhiteSpace(u.Team)))
        {
            LegacyText = LocalizationManager.Format("Team_Legacy", u.School ?? "—", u.Team ?? "—");
        }
        else
        {
            LegacyText = string.Empty;
        }
    }

    // ── loading ─────────────────────────────────────────────────────────────
    private async Task LoadSettingsAsync()
    {
        try
        {
            IsBusy = true;
            _loading = true;

            var dbLanguages = await _apiClient.GetLanguagesAsync();
            AvailableLanguages.Clear();
            foreach (var lang in dbLanguages)
                AvailableLanguages.Add(new LanguageModel { Id = lang.LanguageId, Name = lang.Name });

            _currentUser = await _apiClient.GetCurrentUserAsync();
            if (_currentUser != null)
            {
                FirstName = _currentUser.FirstName;
                // the learner's own picture (while a bought one is worn it is kept in AvatarDefaultSvg)
                var own = _currentUser.AvatarItemKey is null ? _currentUser.AvatarSvg : _currentUser.AvatarDefaultSvg;
                AvatarIndex = own is null ? -1 : Jumbo.Shared.PresetAvatars.All.ToList().IndexOf(own);
                Username = _currentUser.Username ?? string.Empty;
                LastName = _currentUser.LastName;
                Phone = !string.IsNullOrEmpty(_currentUser.Phone) ? _currentUser.Phone : _currentUser.PhoneNumber > 0 ? _currentUser.PhoneNumber.ToString() : string.Empty;
                HideName = _currentUser.HideName;
                HideTooltips = _currentUser.HideTooltips;
                DailyGoalKey = (_currentUser.DailyGoalWords > 0 ? _currentUser.DailyGoalWords : 10).ToString();
                IsLearner = _currentUser.Role == UserRole.Learner;

                SelectedTargetLanguage = AvailableLanguages.FirstOrDefault(l => l.Id == _currentUser.TranslationLanguageId);
                SelectedStudyLanguage = AvailableLanguages.FirstOrDefault(l => l.Id == _currentUser.StudyLanguageId);
                AppLanguageKey = dbLanguages.FirstOrDefault(l => l.LanguageId == _currentUser.AppLanguageId)?.ShorName ?? LocalizationService.CurrentCode;

                PickedRegion = _currentUser.Region ?? string.Empty;
                MyTeam = IsLearner && _currentUser.ClassId is not null ? await _apiClient.GetMyTeamAsync() : null;
                if (MyTeam is null) RebuildTeamTexts();
                await LoadBotAsync();
            }

            ThemeKey = ThemeService.Mode;
            PublicId = _currentUser?.PublicId ?? string.Empty;
            BalanceText = _currentUser is null ? string.Empty
                : LocalizationManager.Get("Money_Balance") + ": " + Jumbo.Shared.Money.Format(_currentUser.Balance) + " " + LocalizationManager.Get("Money_Currency");
            if (_currentUser is not null)
            {
                _livesService.PlanUntil = _currentUser.PlanUntil;
                _livesService.Plan = Jumbo.Shared.Plans.Effective(_currentUser.Plan, _currentUser.PlanUntil, DateTime.UtcNow);
            }
            ShowPlan();
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
        finally
        {
            _loading = false;
            IsBusy = false;
        }
    }

    // ── immediate settings ──────────────────────────────────────────────────
    partial void OnThemeKeyChanged(string value)
    {
        if (_loading || value == ThemeService.Mode) return;
        ThemeService.SetMode(value);          // the whole app switches at once
    }

    partial void OnAppLanguageKeyChanged(string value)
    {
        if (_loading || value == LocalizationService.CurrentCode) return;
        LocalizationService.SetLanguage(value);   // every {u:Tr} text refreshes
        _ = SaveSettings(silent: true);
    }

    partial void OnDailyGoalKeyChanged(string value)
    {
        if (_loading || !int.TryParse(value, out var goal)) return;
        _ = SaveGoalAsync(goal);
    }

    private async Task SaveGoalAsync(int goal)
    {
        try
        {
            GoalMessage = string.Empty;
            await _apiClient.SetDailyGoalAsync(goal);
            if (_currentUser is not null) _currentUser.DailyGoalWords = goal;
            GoalMessage = LocalizationManager.Get("Settings_DailyGoalSaved");
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
    }

    // ── team ────────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task SendTeam()
    {
        if (PickedClassId is not { } classId) return;
        TeamError = TeamMessage = string.Empty;
        try
        {
            IsBusy = true;
            MyTeam = await _apiClient.SelectTeamAsync(classId);
            _currentUser = await _apiClient.GetCurrentUserAsync();
            PickedClassId = null;
            TeamMessage = LocalizationManager.Get("Team_Sent");
        }
        catch (Exception ex)
        {
            var key = $"Team_Err_{ex.Message}";
            var text = LocalizationManager.Get(key);
            TeamError = text == key ? ex.Message : text;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LeaveTeam()
    {
        TeamError = TeamMessage = string.Empty;
        try
        {
            IsBusy = true;
            await _apiClient.LeaveTeamAsync();
            MyTeam = null;
            _currentUser = await _apiClient.GetCurrentUserAsync();
            RebuildTeamTexts();
            TeamMessage = LocalizationManager.Get("Team_Left");
        }
        catch (Exception ex)
        {
            TeamError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── save ────────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task SaveSettings() => await SaveSettings(silent: false);

    private async Task SaveSettings(bool silent)
    {
        if (_currentUser == null || _loading) return;

        try
        {
            _currentUser.FirstName = FirstName;
            _currentUser.LastName = LastName;
            var phone = Phone.Trim();
            _currentUser.Phone = string.IsNullOrEmpty(phone) ? null : phone;
            _ = int.TryParse(new string(phone.Where(char.IsDigit).ToArray()), out int legacyPhone);
            _currentUser.PhoneNumber = legacyPhone;
            _currentUser.HideName = HideName;
            _currentUser.HideTooltips = HideTooltips;

            var languages = await _apiClient.GetLanguagesAsync();
            var appLang = languages.FirstOrDefault(l => l.ShorName == AppLanguageKey);
            if (appLang != null) _currentUser.AppLanguageId = appLang.LanguageId;
            if (SelectedTargetLanguage != null) _currentUser.TranslationLanguageId = SelectedTargetLanguage.Id;
            if (SelectedStudyLanguage != null) _currentUser.StudyLanguageId = SelectedStudyLanguage.Id;
            _currentUser.AppTheme = ThemeKey == "dark" ? "Dark" : "Light";

            // The region is editable only while the learner has no team (the server ignores it otherwise).
            if (_currentUser.ClassId is null && !string.IsNullOrEmpty(PickedRegion)) _currentUser.Region = PickedRegion;

            await _apiClient.UpdateCurrentUserAsync(UpdateUserRequest.FromUser(_currentUser));

            UserLanguagePrefs.Save(_currentUser.AppLanguageId, _currentUser.StudyLanguageId, _currentUser.TranslationLanguageId);

            if (!silent) await AppShell.DisplayToastAsync(LocalizationManager.Get("Settings_SavedMessage"));
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
    }

    [RelayCommand]
    private async Task ScanQr() => await Shell.Current.GoToAsync("ScanQrPage");

    [RelayCommand]
    private async Task Logout()
    {
        bool sure = await Shell.Current.DisplayAlertAsync(LocalizationManager.Get("Settings_LogoutConfirm"), string.Empty,
            LocalizationManager.Get("Settings_Yes"), LocalizationManager.Get("Common_Cancel"));
        if (!sure) return;

        UserSession.AccessToken = null;
        UserSession.UserId = 0;
        _dataLoaded = false;
        _currentUser = null;
        _livesService.Streak = 0;
        _livesService.AvatarSvg = null;
        _livesService.FirstName = string.Empty;
        await Shell.Current.GoToAsync("//RegistrationPage");
    }

    // ── lifecycle ───────────────────────────────────────────────────────────
    [RelayCommand]
    private void NavigatedTo() { }

    [RelayCommand]
    private async Task NavigatedFrom()
    {
        await SaveSettings(silent: true);        // autosave when the tab is left
    }

    [RelayCommand]
    private async Task Appearing()
    {
        if (!UserSession.IsRegistered) return;
        if (!_dataLoaded)
        {
            _dataLoaded = true;
            await Task.Delay(100);
            await LoadSettingsAsync();
        }
        else
        {
            // Back on the tab: the team may have been confirmed meanwhile.
            try
            {
                _currentUser = await _apiClient.GetCurrentUserAsync();
                if (IsLearner && _currentUser?.ClassId is not null) MyTeam = await _apiClient.GetMyTeamAsync();
            }
            catch { /* stale status is fine */ }
        }
    }
}

// Класс-обертка для корректного связывания текста и ID внутри Picker
public class LanguageModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
