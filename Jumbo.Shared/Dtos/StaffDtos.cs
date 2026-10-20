using Jumbo.Shared.Entities;

namespace Jumbo.Shared.Dtos;

// Applicant -> server.
public class CreateStaffRequest
{
    // Teacher or Director.
    public UserRole Role { get; set; }
    public string Region { get; set; } = null!;
    public string SchoolName { get; set; } = null!;
    public string? Phone { get; set; }
    public string? Comment { get; set; }
}

// Server -> applicant / admin.
public class StaffRequestDto
{
    public int Id { get; set; }
    public UserRole Role { get; set; }
    public string Region { get; set; } = null!;
    public string SchoolName { get; set; } = null!;
    public string? Phone { get; set; }
    public string? Comment { get; set; }
    public StaffRequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? AdminNote { get; set; }
    public int? SchoolId { get; set; }

    // Admin view only.
    public int UserId { get; set; }
    public string? ApplicantName { get; set; }
    public string? ApplicantLogin { get; set; }
}

// Admin approves: link to an existing school, or (null) create one from the request / NewSchoolName.
public class ApproveStaffRequest
{
    public int? SchoolId { get; set; }
    public string? NewSchoolName { get; set; }
    public string? Note { get; set; }
}

public class RejectStaffRequest
{
    public string? Note { get; set; }
}

public class SchoolDto
{
    public int SchoolId { get; set; }
    public string Region { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool IsVerified { get; set; }

    // Teachers / directors linked to the school (admin list only).
    public int StaffCount { get; set; }
}

public class CreateSchoolRequest
{
    public string Region { get; set; } = null!;
    public string Name { get; set; } = null!;
}

public class SetVerifiedRequest
{
    public bool IsVerified { get; set; }
}
