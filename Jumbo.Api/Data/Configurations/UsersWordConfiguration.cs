using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jumbo.Api.Data.Configurations;

public class UsersWordConfiguration : IEntityTypeConfiguration<UsersWord>
{
    public void Configure(EntityTypeBuilder<UsersWord> builder)
    {
        builder.HasKey(x => x.UsersWordId);
        builder.HasIndex(x => new { x.UserId, x.WordId }).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Word>()
            .WithMany()
            .HasForeignKey(x => x.WordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
