using Amaliyotchi.Domain.Files;
using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amaliyotchi.Infrastructure.Persistence.Configurations;

public sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.ToTable("app_settings");
        builder.HasKey(x => x.Key);

        builder.Property(x => x.Key).HasMaxLength(AppSetting.KeyMaxLength).ValueGeneratedNever();
        builder.Property(x => x.Value).HasMaxLength(AppSetting.ValueMaxLength).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UpdatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.ToTable("holidays");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(Holiday.NameMaxLength).IsRequired();

        builder.HasIndex(x => x.Date);
    }
}

public sealed class DocumentTemplateConfiguration : IEntityTypeConfiguration<DocumentTemplate>
{
    public void Configure(EntityTypeBuilder<DocumentTemplate> builder)
    {
        builder.ToTable("document_templates");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(DocumentTemplate.NameMaxLength).IsRequired();
        builder.Property(x => x.Kind).HasConversion<int>().IsRequired();

        builder.HasOne<StoredFile>()
            .WithMany()
            .HasForeignKey(x => x.FileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.Kind, x.IsActive });
    }
}
