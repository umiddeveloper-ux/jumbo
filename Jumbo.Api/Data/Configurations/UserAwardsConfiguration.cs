using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jumbo.Api.Data.Configurations;

public class UserAwardsConfiguration : IEntityTypeConfiguration<UserAwards>
{
    public void Configure(EntityTypeBuilder<UserAwards> builder)
    {
        builder.HasKey(x => x.UserAwardsId);
        builder.HasIndex(x => new { x.UserId, x.AwardsId }).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Award>()
            .WithMany()
            .HasForeignKey(x => x.AwardsId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
