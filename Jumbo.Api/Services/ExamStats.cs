using Jumbo.Api.Data;
using Jumbo.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Services;

// The exam figures that the teacher's team exam report and the director's school exam report share, so that both show the same numbers.
public static class ExamStats
{
    /// <summary>
    /// The best result (0..1) of every pupil in every book, over the exams by a teacher's code taken in the given teams. A free exam the pupil
    /// started themselves does not count. Only approved pupils whose team is still the team of the exam.
    /// </summary>
    public static async Task<Dictionary<int, Dictionary<int, double>>> BestByPupilAndBookAsync(AppDbContext db, IReadOnlyCollection<int> classIds)
    {
        var rows = await (
            from r in db.ExamResults.AsNoTracking()
            join u in db.Users.AsNoTracking() on r.UserId equals u.UserId
            where r.TeacherExamId != null && r.TotalCount > 0 && r.ClassId != null && classIds.Contains(r.ClassId.Value)
                  && u.ClassApproved && u.ClassId == r.ClassId
            select new { r.UserId, r.BookNumber, r.TotalCount, r.CorrectCount }).ToListAsync();

        return rows.GroupBy(r => r.UserId).ToDictionary(
            g => g.Key,
            g => g.GroupBy(r => r.BookNumber).ToDictionary(b => b.Key, b => b.Max(r => (double)r.CorrectCount / r.TotalCount)));
    }

    /// <summary>The words learned in one book: WordsPerBook x the best result.</summary>
    public static int WordsOf(double bestRatio) => (int)Math.Round(ExamReport.WordsPerBook * bestRatio);

    /// <summary>The words learned over all the books of a pupil (the "total" column of the team exam report).</summary>
    public static int WordsTotal(Dictionary<int, double>? bestByBook) => bestByBook is null
        ? 0
        : Enumerable.Range(1, Jumbo.Shared.Course.BookCount).Sum(b => bestByBook.TryGetValue(b, out var ratio) ? WordsOf(ratio) : 0);
}
