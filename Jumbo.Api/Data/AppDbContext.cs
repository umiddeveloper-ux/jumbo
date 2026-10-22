using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<SvgImage> SvgImages => Set<SvgImage>();
    public DbSet<Word> Words => Set<Word>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UsersWord> UsersWords => Set<UsersWord>();
    public DbSet<UserSubjectProgress> UserSubjectProgresses => Set<UserSubjectProgress>();
    public DbSet<Award> Awards => Set<Award>();
    public DbSet<UserAwards> UserAwards => Set<UserAwards>();
    public DbSet<Market> Markets => Set<Market>();
    public DbSet<UserMarkets> UserMarkets => Set<UserMarkets>();
    public DbSet<QrLoginSession> QrLoginSessions => Set<QrLoginSession>();
    public DbSet<School> Schools => Set<School>();
    public DbSet<StaffRequest> StaffRequests => Set<StaffRequest>();
    public DbSet<SchoolClass> SchoolClasses => Set<SchoolClass>();
    public DbSet<TeacherExam> TeacherExams => Set<TeacherExam>();
    public DbSet<ExamResult> ExamResults => Set<ExamResult>();
    public DbSet<ExamMistake> ExamMistakes => Set<ExamMistake>();
    public DbSet<SchoolReportLink> SchoolReportLinks => Set<SchoolReportLink>();
    public DbSet<TelegramConnection> TelegramConnections => Set<TelegramConnection>();
    public DbSet<UserDailyWord> UserDailyWords => Set<UserDailyWord>();
    public DbSet<UserDailyProgress> UserDailyProgresses => Set<UserDailyProgress>();
    public DbSet<UserBookUnlock> UserBookUnlocks => Set<UserBookUnlock>();
    public DbSet<Subscriber> Subscribers => Set<Subscriber>();
    public DbSet<AppVersion> AppVersions => Set<AppVersion>();
    public DbSet<BalanceTransaction> BalanceTransactions => Set<BalanceTransaction>();
    public DbSet<JournalMark> JournalMarks => Set<JournalMark>();
    public DbSet<Ad> Ads => Set<Ad>();
    public DbSet<AdView> AdViews => Set<AdView>();
    public DbSet<PlanPrice> PlanPrices => Set<PlanPrice>();
    public DbSet<FeedbackMessage> FeedbackMessages => Set<FeedbackMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
