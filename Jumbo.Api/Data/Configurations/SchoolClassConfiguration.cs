using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jumbo.Api.Data.Configurations;

public class SchoolClassConfiguration : IEntityTypeConfiguration<SchoolClass>
{
    public void Configure(EntityTypeBuilder<SchoolClass> builder)
    {
        builder.HasKey(x => x.ClassId);
        builder.Property(x => x.Name).HasMaxLength(60);
        builder.HasIndex(x => x.TeacherId);
        builder.HasIndex(x => x.SchoolId);

        builder.HasOne<School>()
            .WithMany()
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExamResultConfiguration : IEntityTypeConfiguration<ExamResult>
{
    public void Configure(EntityTypeBuilder<ExamResult> builder)
    {
        builder.HasKey(x => x.ExamResultId);
        builder.Ignore(x => x.Percent);
        builder.HasIndex(x => new { x.UserId, x.TakenAt });
        builder.HasIndex(x => new { x.ClassId, x.TakenAt });
        builder.HasIndex(x => x.TeacherExamId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<SchoolClass>()
            .WithMany()
            .HasForeignKey(x => x.ClassId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<TeacherExam>()
            .WithMany()
            .HasForeignKey(x => x.TeacherExamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TeacherExamConfiguration : IEntityTypeConfiguration<TeacherExam>
{
    public void Configure(EntityTypeBuilder<TeacherExam> builder)
    {
        builder.HasKey(x => x.TeacherExamId);
        builder.Property(x => x.Code).HasMaxLength(6);
        builder.HasIndex(x => new { x.Code, x.ExpiresAt });
        builder.HasIndex(x => new { x.TeacherId, x.CreatedAt });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExamMistakeConfiguration : IEntityTypeConfiguration<ExamMistake>
{
    public void Configure(EntityTypeBuilder<ExamMistake> builder)
    {
        builder.HasKey(x => x.ExamMistakeId);
        builder.Property(x => x.Word).HasMaxLength(100);
        builder.Property(x => x.Translation).HasMaxLength(100);
        builder.HasIndex(x => x.ExamResultId);

        builder.HasOne<ExamResult>()
            .WithMany()
            .HasForeignKey(x => x.ExamResultId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SchoolReportLinkConfiguration : IEntityTypeConfiguration<SchoolReportLink>
{
    public void Configure(EntityTypeBuilder<SchoolReportLink> builder)
    {
        builder.HasKey(x => x.SchoolReportLinkId);
        builder.Property(x => x.Token).HasMaxLength(64);
        builder.HasIndex(x => x.Token).IsUnique();
        builder.HasIndex(x => x.SchoolId);

        builder.HasOne<School>()
            .WithMany()
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
