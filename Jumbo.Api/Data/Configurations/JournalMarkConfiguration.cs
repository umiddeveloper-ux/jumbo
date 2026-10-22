using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jumbo.Api.Data.Configurations;

public class JournalMarkConfiguration : IEntityTypeConfiguration<JournalMark>
{
    public void Configure(EntityTypeBuilder<JournalMark> builder)
    {
        builder.HasKey(x => new { x.UserId, x.SubjectId });
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.SubjectId);
    }
}
