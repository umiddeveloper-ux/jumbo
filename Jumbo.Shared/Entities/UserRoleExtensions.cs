namespace Jumbo.Shared.Entities;

public static class UserRoleExtensions
{
    /// <summary>Teachers and directors work in their cabinet: they do not take the lessons, so the learning pages are hidden from them.</summary>
    public static bool IsStaff(this UserRole? role) => role is UserRole.Teacher or UserRole.Director;
}
