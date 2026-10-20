namespace Jumbo.Shared.Entities;

public enum StaffRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

// "I am a teacher / director at school X": created by a signed-in user, decided by an Admin.
public class StaffRequest
{
    public int StaffRequestId { get; set; }

    public int UserId { get; set; }

    // Teacher or Director.
    public UserRole RequestedRole { get; set; }

    public string Region { get; set; } = null!;

    // The school as the applicant typed it.
    public string SchoolName { get; set; } = null!;

    public string? Phone { get; set; }

    public string? Comment { get; set; }

    public StaffRequestStatus Status { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAt { get; set; }

    public int? ReviewedByUserId { get; set; }

    // Shown to the applicant (e.g. why a request was rejected).
    public string? AdminNote { get; set; }

    // The School the applicant was linked to when approved.
    public int? SchoolId { get; set; }
}
