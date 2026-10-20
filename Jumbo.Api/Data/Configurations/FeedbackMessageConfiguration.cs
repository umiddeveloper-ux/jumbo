using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jumbo.Api.Data.Configurations;

public class FeedbackMessageConfiguration : IEntityTypeConfiguration<FeedbackMessage>
{
    public void Configure(EntityTypeBuilder<FeedbackMessage> builder)
    {
        builder.HasKey(x => x.FeedbackMessageId);
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.Message).HasMaxLength(1000);
        builder.Property(x => x.Lang).HasMaxLength(2);
        builder.HasIndex(x => x.CreatedAt);
    }
}
