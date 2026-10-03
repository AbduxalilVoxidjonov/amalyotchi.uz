using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Domain.Organization;
using Amaliyotchi.Domain.Students;
using Amaliyotchi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amaliyotchi.Infrastructure.Persistence.Configurations;

public sealed class StudentProfileConfiguration : IEntityTypeConfiguration<StudentProfile>
{
    public void Configure(EntityTypeBuilder<StudentProfile> builder)
    {
        builder.ToTable("student_profiles");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.HemisId).HasMaxLength(HemisId.MaxLength).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.InviteToken).HasMaxLength(100);

        // Talabaning o'z ish vaqti (hammasi ixtiyoriy) — StudentProfile.SetWorkHours/HoursOn.
        builder.Property(x => x.WorkStart);
        builder.Property(x => x.WorkEnd);
        builder.Property(x => x.WorkHoursEffectiveFrom);
        builder.Property(x => x.PreviousWorkStart);
        builder.Property(x => x.PreviousWorkEnd);

        // User ↔ StudentProfile 1:1 — bitta foydalanuvchiga bitta profil.
        builder.HasOne(x => x.User)
            .WithOne(u => u.StudentProfile)
            .HasForeignKey<StudentProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Group)
            .WithMany()
            .HasForeignKey(x => x.StudentGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasIndex(x => x.HemisId).IsUnique().HasFilter("is_deleted = false");
        builder.HasIndex(x => x.StudentGroupId);
        builder.HasIndex(x => x.InviteToken).IsUnique().HasFilter("invite_token IS NOT NULL");
    }
}

public sealed class TutorAssignmentConfiguration : IEntityTypeConfiguration<TutorAssignment>
{
    public void Configure(EntityTypeBuilder<TutorAssignment> builder)
    {
        builder.ToTable("tutor_assignments");
        builder.HasKey(x => x.Id);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.TutorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Group)
            .WithMany()
            .HasForeignKey(x => x.StudentGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AcademicYear>()
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TutorUserId, x.StudentGroupId })
            .IsUnique()
            .HasFilter("is_deleted = false");
        builder.HasIndex(x => x.StudentGroupId);
    }
}

public sealed class TutorFacultyConfiguration : IEntityTypeConfiguration<TutorFaculty>
{
    public void Configure(EntityTypeBuilder<TutorFaculty> builder)
    {
        builder.ToTable("tutor_faculties");
        builder.HasKey(x => x.Id);

        // User tomoni UserConfiguration'da (HasMany + backing field). Fakultet o'chsa bog'lanish ham o'chadi.
        builder.HasOne<Faculty>()
            .WithMany()
            .HasForeignKey(x => x.FacultyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TutorUserId, x.FacultyId }).IsUnique();
        // Fakultet bo'yicha tyutorlar (ro'yxat filtri, dashboard, fakultetni o'chirish tekshiruvi).
        builder.HasIndex(x => x.FacultyId);
    }
}

public sealed class TutorScopeConfiguration : IEntityTypeConfiguration<TutorScope>
{
    public void Configure(EntityTypeBuilder<TutorScope> builder)
    {
        builder.ToTable("tutor_scopes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Level).HasConversion<int>().IsRequired();
        builder.Ignore(x => x.NodeId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.TutorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Faculty>()
            .WithMany()
            .HasForeignKey(x => x.FacultyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Direction>()
            .WithMany()
            .HasForeignKey(x => x.DirectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StudentGroup>()
            .WithMany()
            .HasForeignKey(x => x.StudentGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TutorUserId);
        // Kesishmaslik tekshiruvi fakultet bo'yicha faol ko'lamlarni yuklaydi.
        builder.HasIndex(x => x.FacultyId).HasFilter("is_deleted = false");
    }
}
