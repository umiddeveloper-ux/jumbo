using Jumbo.Shared.Entities;

namespace Jumbo.Shared.Dtos;

public class UpdateUserRequest
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public int PhoneNumber { get; set; }
    public int AppLanguageId { get; set; }
    public int StudyLanguageId { get; set; }
    public int TranslationLanguageId { get; set; }
    public int? TranslationLanguageId2 { get; set; }
    public int LifeCount { get; set; }
    public int CurrentBookId { get; set; }
    public DateTime LifeExpirationDate { get; set; }
    public int LpPoints { get; set; }
    public string? CurrentLanguageLevel { get; set; }
    public bool HideTooltips { get; set; }
    public string AppTheme { get; set; } = "Light";
    public string? Phone { get; set; }
    public string? Region { get; set; }
    public string? School { get; set; }
    public string? Team { get; set; }
    public bool HideName { get; set; }

    // Only ever goes down through this request (a shield is used up): null = not sent, a client that does not know shields leaves it alone.
    public int? ShieldCount { get; set; }

    public static UpdateUserRequest FromUser(User user) => new()
    {
        FirstName = user.FirstName,
        LastName = user.LastName,
        PhoneNumber = user.PhoneNumber,
        AppLanguageId = user.AppLanguageId,
        StudyLanguageId = user.StudyLanguageId,
        TranslationLanguageId = user.TranslationLanguageId,
        TranslationLanguageId2 = user.TranslationLanguageId2,
        LifeCount = user.LifeCount,
        CurrentBookId = user.CurrentBookId,
        LifeExpirationDate = user.LifeExpirationDate,
        LpPoints = user.LpPoints,
        CurrentLanguageLevel = user.CurrentLanguageLevel,
        HideTooltips = user.HideTooltips,
        AppTheme = user.AppTheme,
        Phone = user.Phone,
        Region = user.Region,
        School = user.School,
        Team = user.Team,
        HideName = user.HideName,
        ShieldCount = user.ShieldCount
    };
}
