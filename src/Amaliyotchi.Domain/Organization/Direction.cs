using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Organization;

/// <summary>Ta'lim yo'nalishi — fakultet ichida.</summary>
public sealed class Direction : AuditableEntity, ISoftDeletable
{
    private readonly List<StudentGroup> _groups = [];

    private Direction() { }

    public Guid FacultyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public IReadOnlyCollection<StudentGroup> Groups => _groups.AsReadOnly();

    internal static Direction Create(Guid facultyId, string name, string code)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Yo'nalish nomi bo'sh bo'lishi mumkin emas.");
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("Yo'nalish kodi bo'sh bo'lishi mumkin emas.");

        return new Direction { FacultyId = facultyId, Name = name.Trim(), Code = code.Trim() };
    }

    public StudentGroup AddGroup(string name, int course, Guid academicYearId)
    {
        if (_groups.Any(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)
                             && g.AcademicYearId == academicYearId))
            throw new ConflictException($"'{name}' guruhi bu o'quv yilida allaqachon mavjud.");

        var group = StudentGroup.Create(Id, name, course, academicYearId);
        _groups.Add(group);
        return group;
    }
}
