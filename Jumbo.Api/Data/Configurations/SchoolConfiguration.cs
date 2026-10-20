using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jumbo.Api.Data.Configurations;

public class SchoolConfiguration : IEntityTypeConfiguration<School>
{
    public void Configure(EntityTypeBuilder<School> builder)
    {
        builder.HasKey(x => x.SchoolId);
        builder.Property(x => x.Region).HasMaxLength(40);
        builder.Property(x => x.Name).HasMaxLength(150);
        builder.HasIndex(x => new { x.Region, x.Name });
    }
}

public class StaffRequestConfiguration : IEntityTypeConfiguration<StaffRequest>
{
    public void Configure(EntityTypeBuilder<StaffRequest> builder)
    {
        builder.HasKey(x => x.StaffRequestId);
        builder.Property(x => x.Region).HasMaxLength(40);
        builder.Property(x => x.SchoolName).HasMaxLength(150);
        builder.Property(x => x.Phone).HasMaxLength(32);
        builder.Property(x => x.Comment).HasMaxLength(500);
        builder.Property(x => x.AdminNote).HasMaxLength(500);
        builder.HasIndex(x => new { x.UserId, x.Status });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<School>()
            .WithMany()
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
