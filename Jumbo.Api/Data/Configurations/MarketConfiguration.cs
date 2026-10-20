using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jumbo.Api.Data.Configurations;

public class MarketConfiguration : IEntityTypeConfiguration<Market>
{
    public void Configure(EntityTypeBuilder<Market> builder)
    {
        builder.HasKey(x => x.MarketId);

        builder.HasOne<Language>()
            .WithMany()
            .HasForeignKey(x => x.LanguageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SvgImage>()
            .WithMany()
            .HasForeignKey(x => x.SvgImageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
