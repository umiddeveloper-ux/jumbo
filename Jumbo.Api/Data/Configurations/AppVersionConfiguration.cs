using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jumbo.Api.Data.Configurations;

public class AppVersionConfiguration : IEntityTypeConfiguration<AppVersion>
{
    public void Configure(EntityTypeBuilder<AppVersion> builder)
    {
        builder.HasKey(x => x.AppVersionId);
        builder.Property(x => x.Platform).HasMaxLength(16);
        builder.Property(x => x.Url).HasMaxLength(500);
        builder.Property(x => x.NotesRu).HasMaxLength(500);
        builder.Property(x => x.NotesEn).HasMaxLength(500);
        builder.Property(x => x.NotesUz).HasMaxLength(500);
        builder.HasIndex(x => x.Platform).IsUnique();
    }
}
