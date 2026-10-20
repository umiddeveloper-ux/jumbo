using Jumbo.ApiClient;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;

namespace JumboWeb.Services;

public class UserStateService
{
    private readonly LpApiClient _apiClient;
    private User? _currentUser;

    public event Action? OnChange;

    public bool IsRegistered => _currentUser is not null;

    // A teacher / director (approved), or somebody whose request for such a role is still waiting for the Admin.
    public bool IsStaffUser => Jumbo.Shared.Entities.UserRoleExtensions.IsStaff(_currentUser?.Role);
    public bool IsStaffApplicant => _currentUser is { Role: UserRole.Learner } u && (u.StaffCandidate || u.PendingStaffRole is not null);
    // They do not take the lessons: no learning pages, tabs, lives or points for them.
    public bool IsAdminUser => _currentUser?.Role == UserRole.Admin;
    public bool HidesLessons => IsStaffUser || IsStaffApplicant || IsAdminUser;
    public User? CurrentUser => _currentUser;

    /// <summary>The version of the app in force (bronze / silver / gold); a paid one that is over counts as bronze.</summary>
    public string CurrentPlan => Jumbo.Shared.Plans.Effective(_currentUser?.Plan, _currentUser?.PlanUntil, DateTime.UtcNow);

    public UserStateService(LpApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <summary>Reads the learner again (the version and the balance changed) and tells the pages that show them.</summary>
    public async Task RefreshAsync()
    {
        _currentUser = await _apiClient.GetCurrentUserAsync();
        NotifyStateChanged();
    }

    public async Task EnsureLoadedAsync()
    {
        if (_currentUser != null) return;
        _currentUser = await _apiClient.GetCurrentUserAsync();
    }

    public async Task<RegisterUserResponse?> RegisterAsync(
        string firstName, string lastName, string phone,
        int studyLangId, int transLangId,
        string username, string password, string? avatarSvg = null,
        string? region = null, int? classId = null, int appLangId = 2, bool asTeacher = false)
    {
        var phoneNumber = int.TryParse(
            phone.Replace("+", "").Replace(" ", "").Replace("-", ""),
            out int ph) ? ph : 0;

        return await _apiClient.RegisterAsync(new RegisterUserRequest
        {
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber,
            AppLanguageId = appLangId,
            StudyLanguageId = studyLangId,
            TranslationLanguageId = transLangId,
            TranslationLanguageId2 = null,
            Username = username,
            Password = password,
            AvatarSvg = avatarSvg,
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            Region = region,
            ClassId = classId,
            RegisterAsTeacher = asTeacher
        });
    }

    public async Task<RegisterUserResponse?> LoginAsync(string username, string password) =>
        await _apiClient.LoginAsync(new LoginRequest { Username = username, Password = password });

    public async Task UpdateUserAsync(User user)
    {
        await _apiClient.UpdateCurrentUserAsync(UpdateUserRequest.FromUser(user));
        _currentUser = user;
        NotifyStateChanged();
    }

    public void Invalidate() => _currentUser = null;

    private void NotifyStateChanged() => OnChange?.Invoke();
}
