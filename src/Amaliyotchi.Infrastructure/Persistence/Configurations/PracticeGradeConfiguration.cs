using Amaliyotchi.Domain.Grading;
using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Domain.Practice;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amaliyotchi.Infrastructure.Persistence.Configurations;

public sealed class PracticeGradeConfiguration : IEntityTypeConfiguration<PracticeGrade>
{
    public void Configure(EntityTypeBuilder<PracticeGrade> builder)
    {
        builder.ToTable("practice_grades");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Conclusion).HasMaxLength(PracticeGrade.ConclusionMaxLength);

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
            .HasForeignKey(x => x.FinalizedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.StudentUserId, x.PeriodId }).IsUnique();
        builder.HasIndex(x => x.PeriodId);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
