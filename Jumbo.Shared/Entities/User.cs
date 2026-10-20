namespace Jumbo.Shared.Entities;

public class User
{
    public int UserId { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public int PhoneNumber { get; set; }

    public int AppLanguageId { get; set; }

    public int StudyLanguageId { get; set; }

    public int TranslationLanguageId { get; set; }

    public int? TranslationLanguageId2 { get; set; }

    public int LifeCount { get; set; } = 5;

    public int CurrentBookId { get; set; } = 1;

    public DateTime LifeExpirationDate { get; set; }

    public int LpPoints { get; set; } = 100;

    public string? CurrentLanguageLevel { get; set; }

    public bool HideTooltips { get; set; }

    public string AppTheme { get; set; } = "Light";

    public string AccessToken { get; set; } = null!;

    public string? Username { get; set; }

    public string? PasswordHash { get; set; }

    // Либо инлайновая разметка &lt;svg&gt; одного из пресетов, либо data:-URL загруженного изображения.
    public string? AvatarSvg { get; set; }

    // The identifier people read out and that payments use: a letter and 7 digits (A0000001). The database computes it from UserId
    // (a stored column, see UserConfiguration and Jumbo.Shared.PublicId); it is never written from the code.
    public string PublicId { get; set; } = string.Empty;

    // Daily goal (distinct words per day) and the streak of consecutive days it was reached.
    public int DailyGoalWords { get; set; } = 10;

    public int Streak { get; set; }

    public DateOnly? LastGoalDate { get; set; }

    // Contact + optional profile collected at registration (step 1 / step 3). PhoneNumber (int) is the legacy
    // field: real numbers like 998901234567 do not fit into an int, so the full number lives in Phone.
    public string? Phone { get; set; }

    // Region code, see the website's region list (e.g. "tashkent-city"); school / team are free text.
    public string? Region { get; set; }

    public string? School { get; set; }

    public string? Team { get; set; }

    // Leaderboards show "Скрытый игрок" instead of "First L." when this is on.
    public bool HideName { get; set; }

    // Learner by default; Teacher / Director / Admin are granted by an Admin (see StaffRequest).
    public UserRole Role { get; set; } = UserRole.Learner;

    // Registered through the "For teachers" page: a teacher in waiting even before the request is sent. Until an Admin makes them a
    // teacher the site shows them nothing but the request page.
    public bool StaffCandidate { get; set; }

    // Filled in by GET api/users/me only (not stored): the role of an open staff request of this user, until an Admin decides.
    // Such a person is a teacher / director in waiting: the site does not offer them the lessons.
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public UserRole? PendingStaffRole { get; set; }

    // The reference School a Teacher / Director belongs to.
    public int? SchoolId { get; set; }

    // The team a learner picked from the list (null = none). While set, School / Team come from the team and are not
    // editable. ClassApproved is false until the team's Teacher confirms; unapproved learners stay out of reports.
    public int? ClassId { get; set; }

    public bool ClassApproved { get; set; }

    // ── What the learner bought in the market (see MarketController) ──
    // Worn: a frame round the avatar, a badge by the name, a colour theme of the site, a pet next to Jumbo in the games.
    public string? FrameKey { get; set; }

    public string? BadgeKey { get; set; }

    public string? ThemeKey { get; set; }

    public string? PetKey { get; set; }

    // The version of the app (see Jumbo.Shared.Plans): "bronze" (free), "silver", "gold". A paid one is in force until PlanUntil; the API
    // turns an expired one back into bronze, so what the clients read is always the version in force.
    public string Plan { get; set; } = "bronze";

    public DateTime? PlanUntil { get; set; }

    // The learner's money (so'm) for buying the versions: topped up by the learner or by anybody through the ID (the payment services),
    // spent in PlansController.Purchase. Never written through UpdateUserRequest. Every movement is a BalanceTransaction.
    public long Balance { get; set; }

    // A bought avatar replaces AvatarSvg; the learner's own is kept here to be put back.
    public string? AvatarItemKey { get; set; }

    public string? AvatarDefaultSvg { get; set; }

    // Things that are used up: a shield takes the next mistake, a freeze keeps the streak over one missed day.
    public int ShieldCount { get; set; }

    public int FreezeCount { get; set; }

    // Filled in by GET api/users/me only (not stored): the pictures of the worn pet and badge.
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? PetSvg { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? BadgeSvg { get; set; }
}
