using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Organization;

/// <summary>Ta'lim yo'nalishi — kafedra ichida.</summary>
public sealed class Direction : AuditableEntity, ISoftDeletable
{
    private readonly List<StudentGroup> _groups = [];

    private Direction() { }

    public Guid DepartmentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public IReadOnlyCollection<StudentGroup> Groups => _groups.AsReadOnly();

    internal static Direction Create(Guid departmentId, string name, string code)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Yo'nalish nomi bo'sh bo'lishi mumkin emas.");
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("Yo'nalish kodi bo'sh bo'lishi mumkin emas.");

        return new Direction { DepartmentId = departmentId, Name = name.Trim(), Code = code.Trim().ToUpperInvariant() };
    }

    public void Update(string name, string code)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Yo'nalish nomi bo'sh bo'lishi mumkin emas.");
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("Yo'nalish kodi bo'sh bo'lishi mumkin emas.");

        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>Faqat o'zini arxivlaydi — guruhlarini emas. Chaqiruvchi oldindan o'chirilmagan
    /// guruh yo'qligini tekshirishi shart (409 qoidasi Application qatlamida).</summary>
    public void Delete(DateTimeOffset now)
    {
        IsDeleted = true;
        DeletedAt = now;
    }

    public StudentGroup AddGroup(string name, int course, Guid academicYearId)
    {
        if (_groups.Any(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)
                             && g.AcademicYearId == academicYearId))
            throw new ConflictException($"'{name}' guruhi bu yo'nalishda allaqachon mavjud.");

        var group = StudentGroup.Create(Id, name, course, academicYearId);
        _groups.Add(group);
        return group;
    }
}
