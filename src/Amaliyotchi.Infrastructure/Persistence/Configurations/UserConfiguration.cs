using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amaliyotchi.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(200);
        builder.Property(u => u.PhoneNumber).HasMaxLength(20);
        builder.Property(u => u.HemisId).HasMaxLength(HemisId.MaxLength);
        builder.Property(u => u.PasswordHash).HasMaxLength(500);
        builder.Property(u => u.Role).HasConversion<int>().IsRequired();

        // Bir xil telefon ikki marta ro'yxatdan o'tmasin (o'chirilganlar hisobga olinmaydi).
        builder.HasIndex(u => u.PhoneNumber)
            .IsUnique()
            .HasFilter("is_deleted = false AND phone_number IS NOT NULL");

        // Xodim (admin/tyutor) login identifikatori — HEMIS ID. Talabada bu maydon null.
        builder.HasIndex(u => u.HemisId)
            .IsUnique()
            .HasFilter("is_deleted = false AND hemis_id IS NOT NULL");

        builder.HasIndex(u => u.TelegramUserId)
            .IsUnique()
            .HasFilter("telegram_user_id IS NOT NULL");

        // Tyutor/talaba ro'yxatlari doimo fakultet bo'yicha filtrlanadi.
        builder.HasIndex(u => new { u.FacultyId, u.Role });

        builder.HasMany(u => u.RefreshTokens)
            .WithOne()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(u => u.RefreshTokens).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
