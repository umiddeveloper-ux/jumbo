using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jumbo.Api.Data.Configurations;

public class SubscriberConfiguration : IEntityTypeConfiguration<Subscriber>
{
    public void Configure(EntityTypeBuilder<Subscriber> builder)
    {
        builder.HasKey(x => x.SubscriberId);
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.Lang).HasMaxLength(2);
        builder.HasIndex(x => x.Email).IsUnique();
    }
}
