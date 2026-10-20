namespace Jumbo.Shared.Entities;

// Reference list of schools (the basis for teacher / director reports). Learners may still type a free-text school
// in their profile; staff are linked to a real School record by an Admin.
public class School
{
    public int SchoolId { get; set; }

    // Region code (see RegionCodes), e.g. "samarkand".
    public string Region { get; set; } = null!;

    public string Name { get; set; } = null!;

    // Checked by an Admin (as opposed to just created from a request).
    public bool IsVerified { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
