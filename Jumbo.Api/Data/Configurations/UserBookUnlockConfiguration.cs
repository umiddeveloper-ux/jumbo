using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jumbo.Api.Data.Configurations;

public class UserBookUnlockConfiguration : IEntityTypeConfiguration<UserBookUnlock>
{
    public void Configure(EntityTypeBuilder<UserBookUnlock> builder)
    {
        builder.HasKey(x => x.UserBookUnlockId);
        builder.HasIndex(x => new { x.UserId, x.BookNumber }).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
