namespace Jumbo.Shared.Entities;

// A team ("команда") run by a Teacher inside a School, e.g. "А_2020": a cohort named by the year it started, so the
// name stays valid as the pupils move up a grade. Learners pick it from a list; the Teacher approves them
// (see User.ClassApproved), and only approved learners count in reports.
public class SchoolClass
{
    public int ClassId { get; set; }

    public int SchoolId { get; set; }

    // The Teacher (or Director) who owns the class.
    public int TeacherId { get; set; }

    // Shown to learners as their "team"; unique per school among active teams.
    public string Name { get; set; } = null!;

    // Archived teams keep their history but are no longer offered in the picker.
    public bool IsArchived { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
