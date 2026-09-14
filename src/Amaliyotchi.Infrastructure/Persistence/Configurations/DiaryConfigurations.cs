using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Files;
using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Domain.Practice;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amaliyotchi.Infrastructure.Persistence.Configurations;

public sealed class DiaryEntryConfiguration : IEntityTypeConfiguration<DiaryEntry>
{
    public void Configure(EntityTypeBuilder<DiaryEntry> builder)
    {
        builder.ToTable("diary_entries");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Text).HasMaxLength(DiaryEntry.TextMaxLength).IsRequired();
        builder.Property(x => x.Learned).HasMaxLength(DiaryEntry.TextMaxLength);
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.TutorComment).HasMaxLength(DiaryEntry.CommentMaxLength);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.StudentUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PracticePeriod>()
            .WithMany()
            .HasForeignKey(x => x.PeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Attachments)
            .WithOne()
            .HasForeignKey(a => a.DiaryEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.StudentUserId, x.Date }).IsUnique();
        builder.HasIndex(x => new { x.PeriodId, x.Status });
        builder.HasIndex(x => x.SubmittedAt).IsDescending();

        builder.Property<uint>("xmin").IsRowVersion();
    }
}

public sealed class DiaryAttachmentConfiguration : IEntityTypeConfiguration<DiaryAttachment>
{
    public void Configure(EntityTypeBuilder<DiaryAttachment> builder)
    {
        builder.ToTable("diary_attachments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName).HasMaxLength(DiaryAttachment.FileNameMaxLength).IsRequired();

        builder.HasOne<StoredFile>()
            .WithMany()
            .HasForeignKey(x => x.StoredFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.DiaryEntryId, x.StoredFileId }).IsUnique();
    }
}
