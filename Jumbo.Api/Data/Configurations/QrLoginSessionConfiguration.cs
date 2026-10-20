using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jumbo.Api.Data.Configurations;

public class QrLoginSessionConfiguration : IEntityTypeConfiguration<QrLoginSession>
{
    public void Configure(EntityTypeBuilder<QrLoginSession> builder)
    {
        builder.HasKey(x => x.SessionId);
        builder.Property(x => x.SessionId).ValueGeneratedNever();
    }
}
