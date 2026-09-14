using Amaliyotchi.Domain.Files;
using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Domain.Organization;
using Amaliyotchi.Domain.Practice;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amaliyotchi.Infrastructure.Persistence.Configurations;

public sealed class PracticePeriodConfiguration : IEntityTypeConfiguration<PracticePeriod>
{
    public void Configure(EntityTypeBuilder<PracticePeriod> builder)
    {
        builder.ToTable("practice_periods");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(PracticePeriod.NameMaxLength).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.WorkDays).HasConversion<int>().IsRequired();

        builder.HasOne<AcademicYear>()
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Groups)
            .WithOne()
            .HasForeignKey(g => g.PeriodId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Groups).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => new { x.StartDate, x.EndDate });
    }
}

public sealed class PracticePeriodGroupConfiguration : IEntityTypeConfiguration<PracticePeriodGroup>
{
    public void Configure(EntityTypeBuilder<PracticePeriodGroup> builder)
    {
        builder.ToTable("practice_period_groups");
        builder.HasKey(x => x.Id);

        builder.HasOne<StudentGroup>()
            .WithMany()
            .HasForeignKey(x => x.StudentGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.PeriodId, x.StudentGroupId }).IsUnique();
        builder.HasIndex(x => x.StudentGroupId);
    }
}

public sealed class PracticeApplicationConfiguration : IEntityTypeConfiguration<PracticeApplication>
{
    public void Configure(EntityTypeBuilder<PracticeApplication> builder)
    {
        builder.ToTable("practice_applications");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.DecisionComment).HasMaxLength(PracticeApplication.CommentMaxLength);
        builder.Property(x => x.Checklist).HasColumnType("integer[]").IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.StudentUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PracticePeriod>()
            .WithMany()
            .HasForeignKey(x => x.PeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StoredFile>()
            .WithMany()
            .HasForeignKey(x => x.ContractFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.DecidedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.StudentUserId, x.PeriodId });
        builder.HasIndex(x => new { x.PeriodId, x.Status });
        builder.HasIndex(x => x.CompanyId);

        // Bir talaba — bir davrda bitta "tirik" ariza (yuborilgan / qaytarilgan / tasdiqlangan).
        builder.HasIndex(x => new { x.StudentUserId, x.PeriodId })
            .IsUnique()
            .HasDatabaseName("ix_practice_applications_student_period_active")
            .HasFilter(
                $"status IN ({(int)ApplicationStatus.Submitted}, {(int)ApplicationStatus.RevisionNeeded}, {(int)ApplicationStatus.Approved})");

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
