using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jumbo.Api.Data.Configurations;

public class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.HasKey(x => x.SubjectId);

        // BookId intentionally has no FK — there is no Book table (unused in the current app).
        builder.HasOne<SvgImage>()
            .WithMany()
            .HasForeignKey(x => x.SvgImageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
