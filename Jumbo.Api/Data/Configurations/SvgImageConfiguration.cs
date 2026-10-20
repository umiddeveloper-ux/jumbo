using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jumbo.Api.Data.Configurations;

public class SvgImageConfiguration : IEntityTypeConfiguration<SvgImage>
{
    public void Configure(EntityTypeBuilder<SvgImage> builder)
    {
        builder.HasKey(x => x.SvgImageId);
        builder.Property(x => x.SVG).HasColumnType("text");
    }
}
