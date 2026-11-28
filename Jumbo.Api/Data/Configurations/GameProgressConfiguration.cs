using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jumbo.Api.Data.Configurations;

public class GameProgressConfiguration : IEntityTypeConfiguration<GameProgress>
{
    public void Configure(EntityTypeBuilder<GameProgress> builder)
    {
        builder.ToTable("game_progress");
        builder.HasKey(x => x.GameProgressId);
        builder.Property(x => x.Game).HasMaxLength(16);
        builder.Property(x => x.State).HasColumnType("text");

        // one saved game per learner, game and topic (the upsert of the API relies on it)
        builder.HasIndex(x => new { x.UserId, x.Game, x.SubjectId }).IsUnique();
        // the removal of the saved games nobody came back to
        builder.HasIndex(x => x.UpdatedAt);

        // an account that is deleted takes its saved games with it; a topic that is removed with its book too
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Subject>()
            .WithMany()
            .HasForeignKey(x => x.SubjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
