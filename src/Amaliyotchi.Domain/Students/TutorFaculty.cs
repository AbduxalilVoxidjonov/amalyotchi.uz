using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Students;

/// <summary>Tyutor ↔ fakultet bog'lanishi (ko'p-ko'pga). Tyutor bir nechta fakultetga biriktirilishi mumkin — ko'lam
/// (<see cref="TutorScope"/>) faqat shu fakultetlar ichida tanlanadi. Oddiy qator: qo'shiladi/o'chiriladi, tarix
/// saqlanmaydi. Ro'yxatning birinchisi <c>User.FacultyId</c> ("asosiy fakultet") sifatida ham yoziladi — auth/JWT
/// mosligi uchun. Boshqaruv faqat <see cref="Identity.User.SetFaculties"/> orqali.</summary>
public sealed class TutorFaculty : BaseEntity
{
    private TutorFaculty() { }

    public Guid TutorUserId { get; private set; }
    public Guid FacultyId { get; private set; }

    internal static TutorFaculty Create(Guid tutorUserId, Guid facultyId)
    {
        if (tutorUserId == Guid.Empty)
            throw new DomainException("Tyutor ko'rsatilmagan.");
        if (facultyId == Guid.Empty)
            throw new DomainException("Fakultet ko'rsatilmagan.");

        return new TutorFaculty { TutorUserId = tutorUserId, FacultyId = facultyId };
    }
}
