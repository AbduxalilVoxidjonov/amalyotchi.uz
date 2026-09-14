using Amaliyotchi.Domain.Files;
using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Domain.Leave;
using Amaliyotchi.Domain.Practice;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amaliyotchi.Infrastructure.Persistence.Configurations;

public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("leave_requests");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Reason).HasMaxLength(LeaveRequest.ReasonMaxLength).IsRequired();
        builder.Property(x => x.AttachmentName).HasMaxLength(255);
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.DecisionComment).HasMaxLength(LeaveRequest.CommentMaxLength);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.StudentUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PracticePeriod>()
            .WithMany()
            .HasForeignKey(x => x.PeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StoredFile>()
            .WithMany()
            .HasForeignKey(x => x.DocumentFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.DecidedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.StudentUserId, x.DateFrom });
        builder.HasIndex(x => new { x.PeriodId, x.Status });

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
