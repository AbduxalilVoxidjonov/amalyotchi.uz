using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Organization;

public sealed class Faculty : AuditableEntity, ISoftDeletable
{
    private readonly List<Direction> _directions = [];

    private Faculty() { }

    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public IReadOnlyCollection<Direction> Directions => _directions.AsReadOnly();

    public static Faculty Create(string name, string code)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Fakultet nomi bo'sh bo'lishi mumkin emas.");
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("Fakultet kodi bo'sh bo'lishi mumkin emas.");

        return new Faculty { Name = name.Trim(), Code = code.Trim().ToUpperInvariant() };
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Fakultet nomi bo'sh bo'lishi mumkin emas.");
        Name = name.Trim();
    }

    public Direction AddDirection(string name, string code)
    {
        if (_directions.Any(d => string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException($"'{code}' kodli yo'nalish bu fakultetda allaqachon mavjud.");

        var direction = Direction.Create(Id, name, code);
        _directions.Add(direction);
        return direction;
    }
}
