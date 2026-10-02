using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amaliyotchi.Infrastructure.Persistence.Configurations;

public sealed class BroadcastMessageConfiguration : IEntityTypeConfiguration<BroadcastMessage>
{
    public void Configure(EntityTypeBuilder<BroadcastMessage> builder)
    {
        builder.ToTable("broadcast_messages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Text).HasMaxLength(BroadcastMessage.TextMaxLength).IsRequired();
        builder.Property(x => x.AudienceKind).HasConversion<int>().IsRequired();
        builder.Property(x => x.AudienceLabel).HasMaxLength(BroadcastMessage.AudienceLabelMaxLength).IsRequired();
        builder.Property(x => x.AudienceJson).HasColumnType("jsonb").IsRequired();

        // Tarix: yangi birinchi.
        builder.HasIndex(x => x.CreatedAt);
    }
}

/// <summary>Yetkazishlar — DB-navbat. Holat sonlari xabar qatorida denormalizatsiya qilinmaydi: ular
/// <c>(message_id, status)</c> indeksi bo'yicha GROUP BY bilan hisoblanadi — dispetcher xabar qatoriga yozmaydi
/// (qulf/raqobat yo'q), purge-demo yetkazishlarni o'chirsa ham sonlar o'z-o'zidan to'g'ri qoladi.</summary>
public sealed class BroadcastDeliveryConfiguration : IEntityTypeConfiguration<BroadcastDelivery>
{
    public void Configure(EntityTypeBuilder<BroadcastDelivery> builder)
    {
        builder.ToTable("broadcast_deliveries");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.Error).HasMaxLength(BroadcastDelivery.ErrorMaxLength);

        // Xabar bilan birga o'chadi (xabarning o'zi o'chirilmaydi — faqat to'liq tozalash uchun).
        builder.HasOne<BroadcastMessage>()
            .WithMany()
            .HasForeignKey(x => x.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: foydalanuvchi jismonan o'chirilishidan oldin (purge-demo) yetkazishlar aniq o'chiriladi.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.RecipientUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Dispetcher: status = pending AND next_attempt_at <= now.
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt });
        // Xabar bo'yicha sonlar (GROUP BY status) va holat filtri.
        builder.HasIndex(x => new { x.MessageId, x.Status });
        // Bir xabar bitta talabaga bir marta.
        builder.HasIndex(x => new { x.MessageId, x.RecipientUserId }).IsUnique();
        builder.HasIndex(x => x.RecipientUserId);
    }
}
