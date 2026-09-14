using Amaliyotchi.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amaliyotchi.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.EntityName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(64);
        builder.Property(a => a.Action).HasConversion<int>().IsRequired();
        builder.Property(a => a.UserRole).HasConversion<int>();
        builder.Property(a => a.Reason).HasMaxLength(500);
        builder.Property(a => a.IpAddress).HasMaxLength(64);
        builder.Property(a => a.TraceId).HasMaxLength(64);

        // O'zgarishlar JSON ko'rinishida: keyinchalik jsonb bo'yicha qidirish mumkin.
        builder.Property(a => a.Changes).HasColumnType("jsonb");

        builder.HasIndex(a => a.OccurredAt).IsDescending();
        builder.HasIndex(a => new { a.EntityName, a.EntityId });
        builder.HasIndex(a => a.UserId);
    }
}
