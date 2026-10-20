using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jumbo.Api.Data.Configurations;

public class UserMarketsConfiguration : IEntityTypeConfiguration<UserMarkets>
{
    public void Configure(EntityTypeBuilder<UserMarkets> builder)
    {
        builder.HasKey(x => x.UserMarketsId);
        builder.HasIndex(x => new { x.UserId, x.MarketId }).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Market>()
            .WithMany()
            .HasForeignKey(x => x.MarketId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
