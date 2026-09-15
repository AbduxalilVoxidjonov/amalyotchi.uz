using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Organization;

/// <summary>Akademik guruh, masalan "412-22".</summary>
public sealed class StudentGroup : AuditableEntity, ISoftDeletable
{
    public const int MinCourse = 1;
    public const int MaxCourse = 6;

    private StudentGroup() { }

    public Guid DirectionId { get; private set; }
    public Guid AcademicYearId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Course { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    internal static StudentGroup Create(Guid directionId, string name, int course, Guid academicYearId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Guruh nomi bo'sh bo'lishi mumkin emas.");
        if (course is < MinCourse or > MaxCourse)
            throw new DomainException($"Kurs {MinCourse} va {MaxCourse} oralig'ida bo'lishi kerak.");

        return new StudentGroup
        {
            DirectionId = directionId,
            AcademicYearId = academicYearId,
            Name = name.Trim(),
            Course = course
        };
    }

    public void Update(string name, int course)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Guruh nomi bo'sh bo'lishi mumkin emas.");
        if (course is < MinCourse or > MaxCourse)
            throw new DomainException($"Kurs {MinCourse} va {MaxCourse} oralig'ida bo'lishi kerak.");

        Name = name.Trim();
        Course = course;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>O'zini arxivlaydi. Chaqiruvchi oldindan talaba/faol tyutor biriktiruvi yo'qligini
    /// tekshirishi shart (409 qoidasi Application qatlamida).</summary>
    public void Delete(DateTimeOffset now)
    {
        IsDeleted = true;
        DeletedAt = now;
    }

    /// <summary>Yangi o'quv yiliga ko'chirish: kurs bittaga oshadi.</summary>
    public StudentGroup PromoteTo(Guid nextAcademicYearId)
    {
        if (Course >= MaxCourse)
            throw new DomainException("Bitiruvchi guruhni keyingi kursga ko'chirib bo'lmaydi.");

        return Create(DirectionId, Name, Course + 1, nextAcademicYearId);
    }
}
