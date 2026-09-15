using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Organization;

/// <summary>Kafedra — fakultet ichida, yo'nalishlarning otasi.</summary>
public sealed class Department : AuditableEntity, ISoftDeletable
{
    private readonly List<Direction> _directions = [];

    private Department() { }

    public Guid FacultyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public IReadOnlyCollection<Direction> Directions => _directions.AsReadOnly();

    internal static Department Create(Guid facultyId, string name, string code)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Kafedra nomi bo'sh bo'lishi mumkin emas.");
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("Kafedra kodi bo'sh bo'lishi mumkin emas.");

        return new Department { FacultyId = facultyId, Name = name.Trim(), Code = code.Trim().ToUpperInvariant() };
    }

    public void Update(string name, string code)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Kafedra nomi bo'sh bo'lishi mumkin emas.");
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("Kafedra kodi bo'sh bo'lishi mumkin emas.");

        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>Faqat o'zini arxivlaydi — bolalarini emas. Chaqiruvchi oldindan bolasi yo'qligini tekshirishi shart
    /// (409 qoidasi Application qatlamida).</summary>
    public void Delete(DateTimeOffset now)
    {
        IsDeleted = true;
        DeletedAt = now;
    }

    public Direction AddDirection(string name, string code)
    {
        if (_directions.Any(d => string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException($"'{code}' kodli yo'nalish bu kafedrada allaqachon mavjud.");

        var direction = Direction.Create(Id, name, code);
        _directions.Add(direction);
        return direction;
    }
}
