using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jumbo.Api.Data.Configurations;

public class AdConfiguration : IEntityTypeConfiguration<Ad>
{
    public void Configure(EntityTypeBuilder<Ad> builder)
    {
        builder.HasKey(x => x.AdId);
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.Property(x => x.Html).HasMaxLength(Jumbo.Shared.SafeHtml.MaxLength);
        builder.Property(x => x.ImageType).HasMaxLength(32);
        builder.Property(x => x.Mode).HasMaxLength(16);
        builder.Property(x => x.Lang).HasMaxLength(2);
        builder.HasIndex(x => new { x.IsActive, x.StartsAt });
    }
}

public class AdViewConfiguration : IEntityTypeConfiguration<AdView>
{
    public void Configure(EntityTypeBuilder<AdView> builder)
    {
        builder.HasKey(x => new { x.UserId, x.AdId });
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Ad>().WithMany().HasForeignKey(x => x.AdId).OnDelete(DeleteBehavior.Cascade);
    }
}
