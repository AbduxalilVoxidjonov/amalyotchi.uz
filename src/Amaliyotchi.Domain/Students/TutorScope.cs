using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Students;

/// <summary>Tyutorning IERARXIK ko'lami — admin tanlagan tugun: fakultet, kafedra, yo'nalish yoki guruh.
/// Ko'lam guruhlarga <em>materializatsiya</em> qilinadi (<c>TutorAssignment</c>) — scoping/statistika o'sha
/// yozuvlarga tayanadi, bu entity faqat "nima tanlangan"ni saqlaydi. Ota id'lar denormalizatsiya qilingan:
/// guruh ko'lamida 4 tasi ham to'la, yo'nalishda 3 tasi, kafedrada 2 tasi, fakultetda faqat <see cref="FacultyId"/>.
/// Ikki xil tyutorning faol ko'lamlari kesishmasligi kerak (<see cref="Overlaps"/>) — Application qatlamida 409.</summary>
public sealed class TutorScope : AuditableEntity, ISoftDeletable
{
    private TutorScope() { }

    public Guid TutorUserId { get; private set; }
    public TutorScopeLevel Level { get; private set; }
    public Guid FacultyId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public Guid? DirectionId { get; private set; }
    public Guid? StudentGroupId { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Tanlangan tugunning id'si (darajaga qarab).</summary>
    public Guid NodeId => Level switch
    {
        TutorScopeLevel.Faculty => FacultyId,
        TutorScopeLevel.Department => DepartmentId!.Value,
        TutorScopeLevel.Direction => DirectionId!.Value,
        _ => StudentGroupId!.Value
    };

    public static TutorScope Create(
        Guid tutorUserId, TutorScopeLevel level, Guid facultyId,
        Guid? departmentId = null, Guid? directionId = null, Guid? studentGroupId = null)
    {
        if (tutorUserId == Guid.Empty)
            throw new DomainException("Tyutor ko'rsatilmagan.");
        if (!Enum.IsDefined(level))
            throw new DomainException("Ko'lam darajasi noto'g'ri.");
        if (facultyId == Guid.Empty)
            throw new DomainException("Fakultet ko'rsatilmagan.");

        var needsDepartment = level >= TutorScopeLevel.Department;
        var needsDirection = level >= TutorScopeLevel.Direction;
        var needsGroup = level == TutorScopeLevel.Group;

        if (needsDepartment ? departmentId is null || departmentId == Guid.Empty : departmentId is not null)
            throw new DomainException("Kafedra ko'lam darajasiga mos kelmaydi.");
        if (needsDirection ? directionId is null || directionId == Guid.Empty : directionId is not null)
            throw new DomainException("Yo'nalish ko'lam darajasiga mos kelmaydi.");
        if (needsGroup ? studentGroupId is null || studentGroupId == Guid.Empty : studentGroupId is not null)
            throw new DomainException("Guruh ko'lam darajasiga mos kelmaydi.");

        return new TutorScope
        {
            TutorUserId = tutorUserId,
            Level = level,
            FacultyId = facultyId,
            DepartmentId = departmentId,
            DirectionId = directionId,
            StudentGroupId = studentGroupId,
            IsActive = true
        };
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    /// <summary>Ikki ko'lam bir xil tugunni bildiradimi (daraja + tugun id).</summary>
    public bool SameNode(TutorScope other) => Level == other.Level && NodeId == other.NodeId;

    /// <summary>Ko'lam berilgan guruhni (uning ota zanjiri bilan) qamrab oladimi.</summary>
    public bool CoversGroup(Guid facultyId, Guid departmentId, Guid directionId, Guid groupId) => Level switch
    {
        TutorScopeLevel.Faculty => FacultyId == facultyId,
        TutorScopeLevel.Department => DepartmentId == departmentId,
        TutorScopeLevel.Direction => DirectionId == directionId,
        _ => StudentGroupId == groupId
    };

    /// <summary>Sof funksiya: ikki ko'lam kesishadimi — teng, ota/bola yoki bola/ota. Kengroq darajada
    /// (<c>min(a.Level, b.Level)</c>) id'lar teng bo'lsa kesishadi: masalan fakultet ko'lami shu fakultetdagi
    /// har qanday guruh ko'lami bilan kesishadi; ikki xil kafedra — kesishmaydi.</summary>
    public static bool Overlaps(TutorScope a, TutorScope b)
    {
        var level = a.Level < b.Level ? a.Level : b.Level;
        return level switch
        {
            TutorScopeLevel.Faculty => a.FacultyId == b.FacultyId,
            TutorScopeLevel.Department => a.DepartmentId == b.DepartmentId,
            TutorScopeLevel.Direction => a.DirectionId == b.DirectionId,
            _ => a.StudentGroupId == b.StudentGroupId
        };
    }

    /// <summary><paramref name="parent"/> ko'lami <paramref name="child"/> ni to'liq o'z ichiga oladimi
    /// (teng bo'lsa ham <c>true</c>).</summary>
    public static bool Covers(TutorScope parent, TutorScope child) => parent.Level <= child.Level && Overlaps(parent, child);

    /// <summary>Sof funksiya: bir tyutorning tanlovlari ichida ota tanlangan bo'lsa bolalari jimgina tashlab
    /// yuboriladi, takrorlar (bir xil tugun) bittaga keltiriladi. Tartib saqlanadi.</summary>
    public static IReadOnlyList<TutorScope> Normalize(IReadOnlyList<TutorScope> scopes)
    {
        var result = new List<TutorScope>(scopes.Count);
        foreach (var scope in scopes)
        {
            if (result.Any(kept => kept.SameNode(scope)))
                continue;
            if (scopes.Any(other => !ReferenceEquals(other, scope) && !other.SameNode(scope) && Covers(other, scope)))
                continue;
            result.Add(scope);
        }

        return result;
    }
}
