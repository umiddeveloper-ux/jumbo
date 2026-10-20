using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jumbo.Api.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.AppTheme).HasDefaultValue("Light");
        builder.Property(x => x.DailyGoalWords).HasDefaultValue(10);
        builder.Property(x => x.Phone).HasMaxLength(32);
        builder.Property(x => x.Role).HasDefaultValue(UserRole.Learner);
        builder.HasOne<School>().WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<SchoolClass>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.ClassId);
        builder.Property(x => x.Region).HasMaxLength(40);
        builder.Property(x => x.School).HasMaxLength(150);
        builder.Property(x => x.Team).HasMaxLength(100);
        builder.HasIndex(x => x.AccessToken).IsUnique();
        builder.HasIndex(x => x.Username).IsUnique();
        // A0000001 = a letter (one per 10 million ids) + the id in 7 digits; the same rule as Jumbo.Shared.PublicId.Of
        builder.Property(x => x.PublicId).HasMaxLength(8)
            .HasComputedColumnSql("chr(65 + user_id / 10000000) || lpad((user_id % 10000000)::text, 7, '0')", stored: true);
        builder.HasIndex(x => x.PublicId).IsUnique();

        builder.HasOne<Language>()
            .WithMany()
            .HasForeignKey(x => x.AppLanguageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Language>()
            .WithMany()
            .HasForeignKey(x => x.StudyLanguageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Language>()
            .WithMany()
            .HasForeignKey(x => x.TranslationLanguageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Language>()
            .WithMany()
            .HasForeignKey(x => x.TranslationLanguageId2)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
