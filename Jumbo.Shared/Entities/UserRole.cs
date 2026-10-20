namespace Jumbo.Shared.Entities;

// One account, one role. Everybody starts as a Learner; staff roles are never self-assigned:
// they are granted by an Admin after a staff request is approved.
public enum UserRole
{
    Learner = 0,
    Teacher = 1,
    Director = 2,
    Admin = 3
}
