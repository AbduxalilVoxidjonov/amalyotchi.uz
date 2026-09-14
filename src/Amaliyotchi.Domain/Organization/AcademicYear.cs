using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Organization;

/// <summary>O'quv yili: "2026-2027". Bir vaqtda faqat bittasi faol bo'ladi.</summary>
public sealed class AcademicYear : AuditableEntity, ISoftDeletable
{
    private AcademicYear() { }

    public string Name { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public static AcademicYear Create(string name, DateOnly startDate, DateOnly endDate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("O'quv yili nomi bo'sh bo'lishi mumkin emas.");
        if (endDate <= startDate)
            throw new DomainException("Tugash sanasi boshlanish sanasidan keyin bo'lishi kerak.");

        return new AcademicYear { Name = name.Trim(), StartDate = startDate, EndDate = endDate };
    }

    public void Activate() => IsActive = true;

    /// <summary>Arxivlash — o'chirish emas: faqat faollik holati o'zgaradi, IsDeleted ga tegilmaydi.</summary>
    public void Archive() => IsActive = false;
}
