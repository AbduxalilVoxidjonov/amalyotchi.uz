using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;

namespace Amaliyotchi.Domain.Students;

/// <summary>Tyutor ↔ guruh biriktiruvi. Tyutorning ma'lumot ko'lami (qaysi talabalarni ko'radi)
/// aynan shu yozuvlardan hisoblanadi — <c>User.FacultyId</c> faqat qo'pol chegara.</summary>
public sealed class TutorAssignment : AuditableEntity, ISoftDeletable
{
    private TutorAssignment() { }

    public Guid TutorUserId { get; private set; }
    public Guid StudentGroupId { get; private set; }
    public StudentGroup Group { get; private set; } = null!;
    public Guid AcademicYearId { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public static TutorAssignment Create(Guid tutorUserId, Guid studentGroupId, Guid academicYearId)
    {
        if (tutorUserId == Guid.Empty)
            throw new DomainException("Tyutor ko'rsatilmagan.");
        if (studentGroupId == Guid.Empty)
            throw new DomainException("Guruh ko'rsatilmagan.");
        if (academicYearId == Guid.Empty)
            throw new DomainException("O'quv yili ko'rsatilmagan.");

        return new TutorAssignment
        {
            TutorUserId = tutorUserId,
            StudentGroupId = studentGroupId,
            AcademicYearId = academicYearId,
            IsActive = true
        };
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
