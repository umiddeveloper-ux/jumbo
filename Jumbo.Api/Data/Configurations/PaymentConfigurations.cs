using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jumbo.Api.Data.Configurations;

public class PaymentOrderConfiguration : IEntityTypeConfiguration<PaymentOrder>
{
    public void Configure(EntityTypeBuilder<PaymentOrder> builder)
    {
        builder.HasKey(x => x.PaymentOrderId);
        builder.Property(x => x.Plan).HasMaxLength(16);
        builder.Property(x => x.Period).HasMaxLength(16);
        builder.Property(x => x.Status).HasMaxLength(16);
        builder.Property(x => x.PlanBefore).HasMaxLength(16);
        // a deleted account leaves its orders (and their Payme transactions) behind for the accounts of the business, without the learner
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => new { x.UserId, x.PaymentOrderId });
    }
}

public class PaymeTransactionConfiguration : IEntityTypeConfiguration<PaymeTransaction>
{
    public void Configure(EntityTypeBuilder<PaymeTransaction> builder)
    {
        builder.HasKey(x => x.PaymeTransactionId);
        builder.Property(x => x.PaymeId).HasMaxLength(64);
        builder.HasOne<PaymentOrder>().WithMany().HasForeignKey(x => x.PaymentOrderId).OnDelete(DeleteBehavior.Restrict);

        // the same call of Payme (a repeat) finds the same row
        builder.HasIndex(x => x.PaymeId).IsUnique();
        // an order has at most ONE live transaction: two payments of one order can never both be created (the database says no, whatever the code does)
        builder.HasIndex(x => x.PaymentOrderId).IsUnique().HasFilter("state IN (1, 2)");
        // GetStatement searches by Payme's time; the timeout job looks for old created transactions
        builder.HasIndex(x => x.TimeMs);
        builder.HasIndex(x => new { x.State, x.CreateTimeMs });
    }
}
