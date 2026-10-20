using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jumbo.Api.Data.Configurations;

public class BalanceTransactionConfiguration : IEntityTypeConfiguration<BalanceTransaction>
{
    public void Configure(EntityTypeBuilder<BalanceTransaction> builder)
    {
        builder.HasKey(x => x.BalanceTransactionId);
        builder.Property(x => x.Kind).HasMaxLength(16);
        builder.Property(x => x.Provider).HasMaxLength(16);
        builder.Property(x => x.ExternalId).HasMaxLength(100);
        builder.Property(x => x.Note).HasMaxLength(200);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.UserId, x.BalanceTransactionId });
        // one payment of a service is counted once
        builder.HasIndex(x => new { x.Provider, x.ExternalId }).IsUnique().HasFilter("external_id IS NOT NULL");
    }
}

public class PlanPriceConfiguration : IEntityTypeConfiguration<PlanPrice>
{
    public void Configure(EntityTypeBuilder<PlanPrice> builder)
    {
        builder.HasKey(x => new { x.Plan, x.Period });
        builder.Property(x => x.Plan).HasMaxLength(16);
        builder.Property(x => x.Period).HasMaxLength(16);
    }
}
