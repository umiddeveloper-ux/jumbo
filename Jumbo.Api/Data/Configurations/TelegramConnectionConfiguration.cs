using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jumbo.Api.Data.Configurations;

public class TelegramConnectionConfiguration : IEntityTypeConfiguration<TelegramConnection>
{
    public void Configure(EntityTypeBuilder<TelegramConnection> builder)
    {
        builder.HasKey(x => x.TelegramConnectionId);
        builder.Property(x => x.Code).HasMaxLength(32);
        builder.Property(x => x.TelegramName).HasMaxLength(100);
        builder.Property(x => x.Lang).HasMaxLength(4);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => x.ChatId);
        builder.HasIndex(x => x.UserId);

        builder.HasOne<School>()
            .WithMany()
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
