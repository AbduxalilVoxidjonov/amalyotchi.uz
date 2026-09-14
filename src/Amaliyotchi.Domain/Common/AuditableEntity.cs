namespace Amaliyotchi.Domain.Common;

/// <summary>Kim va qachon yaratgani/o'zgartirgani yoziladigan entity.
/// Bu maydonlarni handler emas, SaveChanges interceptor to'ldiradi.</summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
