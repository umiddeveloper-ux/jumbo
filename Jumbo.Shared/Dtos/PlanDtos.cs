namespace Jumbo.Shared.Dtos;

/// <summary>Admin: give a user a version until a date (payment is not built yet, the admin does it by hand).</summary>
public class SetPlanRequest
{
    /// <summary>The login or the ID (A0000001) of the user.</summary>
    public string Username { get; set; } = string.Empty;
    public string Plan { get; set; } = "bronze";
    public DateTime? Until { get; set; }
}

public class PlanUserDto
{
    public int UserId { get; set; }
    public string PublicId { get; set; } = string.Empty;
    public long Balance { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string Plan { get; set; } = "bronze";
    public DateTime? PlanUntil { get; set; }
}
