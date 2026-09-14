using Amaliyotchi.Domain.Files;
using Amaliyotchi.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amaliyotchi.Infrastructure.Persistence.Configurations;

public sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.ToTable("stored_files");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Kind).HasConversion<int>().IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(StoredFile.FileNameMaxLength).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(StoredFile.ContentTypeMaxLength).IsRequired();
        builder.Property(x => x.StoragePath).HasMaxLength(StoredFile.StoragePathMaxLength).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.StoragePath).IsUnique();
        builder.HasIndex(x => new { x.UploadedByUserId, x.UploadedAt });
    }
}
