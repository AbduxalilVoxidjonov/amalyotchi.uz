using Amaliyotchi.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amaliyotchi.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Token).HasMaxLength(200).IsRequired();
        builder.Property(t => t.CreatedByIp).HasMaxLength(64);
        builder.Property(t => t.RevokedReason).HasMaxLength(200);
        builder.Property(t => t.ReplacedByToken).HasMaxLength(200);

        builder.HasIndex(t => t.Token).IsUnique();
        builder.HasIndex(t => new { t.UserId, t.ExpiresAt });
    }
}
