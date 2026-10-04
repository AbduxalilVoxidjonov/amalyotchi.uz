using Amaliyotchi.Domain.Faces;
using Amaliyotchi.Domain.Files;
using Amaliyotchi.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amaliyotchi.Infrastructure.Persistence.Configurations;

public sealed class StudentFaceEnrollmentConfiguration : IEntityTypeConfiguration<StudentFaceEnrollment>
{
    public void Configure(EntityTypeBuilder<StudentFaceEnrollment> builder)
    {
        builder.ToTable("student_face_enrollments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        // SFace embedding — Postgres real[] (Npgsql float[] ni to'g'ridan-to'g'ri xaritalaydi).
        builder.Property(x => x.Embedding).HasColumnType("real[]").IsRequired();
        builder.Property(x => x.RejectReason).HasMaxLength(StudentFaceEnrollment.RejectReasonMaxLength);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.StudentUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StoredFile>()
            .WithMany()
            .HasForeignKey(x => x.PhotoFileId)
            .OnDelete(DeleteBehavior.Restrict);

        // Har talabaga bitta faol etalon.
        builder.HasIndex(x => x.StudentUserId).IsUnique();
        builder.HasIndex(x => new { x.Status, x.SubmittedAt });
        builder.HasIndex(x => x.PhotoFileId);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
