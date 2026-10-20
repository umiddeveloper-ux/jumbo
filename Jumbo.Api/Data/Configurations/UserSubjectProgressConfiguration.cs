using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jumbo.Api.Data.Configurations;

public class UserSubjectProgressConfiguration : IEntityTypeConfiguration<UserSubjectProgress>
{
    public void Configure(EntityTypeBuilder<UserSubjectProgress> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.UserId, x.SubjectId }).IsUnique();
        builder.Ignore(x => x.AllWon);
        builder.Ignore(x => x.PartsDone);
        builder.Ignore(x => x.PhraseCounted);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Subject>()
            .WithMany()
            .HasForeignKey(x => x.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
