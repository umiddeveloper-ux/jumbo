using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jumbo.Api.Data.Configurations;

public class PlacementPassageConfiguration : IEntityTypeConfiguration<PlacementPassage>
{
    public void Configure(EntityTypeBuilder<PlacementPassage> builder)
    {
        builder.HasKey(x => x.PlacementPassageId);
        builder.Property(x => x.Code).HasMaxLength(40);
        builder.Property(x => x.Level).HasMaxLength(2);
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public class PlacementQuestionConfiguration : IEntityTypeConfiguration<PlacementQuestion>
{
    public void Configure(EntityTypeBuilder<PlacementQuestion> builder)
    {
        builder.HasKey(x => x.PlacementQuestionId);
        builder.Property(x => x.Code).HasMaxLength(40);
        builder.Property(x => x.Skill).HasMaxLength(20);
        builder.Property(x => x.Level).HasMaxLength(2);
        builder.Property(x => x.Prompt).HasMaxLength(1000);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.Level, x.Skill });

        builder.HasOne<PlacementPassage>()
            .WithMany()
            .HasForeignKey(x => x.PlacementPassageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
