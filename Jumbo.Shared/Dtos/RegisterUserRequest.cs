namespace Jumbo.Shared.Dtos;

public class RegisterUserRequest
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public int PhoneNumber { get; set; }
    public int AppLanguageId { get; set; }
    public int StudyLanguageId { get; set; }
    public int TranslationLanguageId { get; set; }
    public int? TranslationLanguageId2 { get; set; }
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string? AvatarSvg { get; set; }
    public string? Phone { get; set; }
    public string? Region { get; set; }
    public string? School { get; set; }
    public string? Team { get; set; }

    // Optional: the team picked from the school list (its school comes with it). Replaces free-text School / Team,
    // which the server no longer accepts.
    public int? ClassId { get; set; }

    // Set by the website when the person registers through the "For teachers" page.
    public bool RegisterAsTeacher { get; set; }
}
