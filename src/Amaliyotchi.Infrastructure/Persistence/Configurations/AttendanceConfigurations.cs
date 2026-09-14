using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Domain.Leave;
using Amaliyotchi.Domain.Practice;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amaliyotchi.Infrastructure.Persistence.Configurations;

public sealed class DailyAttendanceConfiguration : IEntityTypeConfiguration<DailyAttendance>
{
    public void Configure(EntityTypeBuilder<DailyAttendance> builder)
    {
        builder.ToTable("daily_attendances");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.SuspiciousReason).HasMaxLength(DailyAttendance.ReasonMaxLength);
        builder.Property(x => x.ManualReason).HasMaxLength(DailyAttendance.ReasonMaxLength);

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
            .HasForeignKey(x => x.ManualByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<LeaveRequest>()
            .WithMany()
            .HasForeignKey(x => x.LeaveRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.StudentUserId, x.Date }).IsUnique();
        builder.HasIndex(x => new { x.PeriodId, x.Date });
        builder.HasIndex(x => x.Date);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}

public sealed class AttendanceEventConfiguration : IEntityTypeConfiguration<AttendanceEvent>
{
    public void Configure(EntityTypeBuilder<AttendanceEvent> builder)
    {
        builder.ToTable("attendance_events");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Kind).HasConversion<int>().IsRequired();
        builder.Property(x => x.RejectReason).HasConversion<int>().IsRequired();

        builder.ComplexProperty(x => x.Location, location =>
        {
            location.Property(p => p.Latitude).HasColumnName("latitude").IsRequired();
            location.Property(p => p.Longitude).HasColumnName("longitude").IsRequired();
        });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.StudentUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.Date, x.StudentUserId });
        builder.HasIndex(x => new { x.StudentUserId, x.ReceivedAt }).IsDescending(false, true);
    }
}
