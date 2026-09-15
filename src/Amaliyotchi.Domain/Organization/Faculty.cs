using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Organization;

public sealed class Faculty : AuditableEntity, ISoftDeletable
{
    private readonly List<Direction> _directions = [];

    private Faculty() { }

    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;

    /// <summary>Faol emas fakultet ro'yxatda qoladi (o'chirilmagan), lekin faoliyat ko'rsatmaydi deb
    /// belgilanadi — masalan yopilgan yoki vaqtincha to'xtatilgan. Soft-delete bilan aralashtirilmasin.</summary>
    public bool IsActive { get; private set; } = true;
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

    public void ChangeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("Fakultet kodi bo'sh bo'lishi mumkin emas.");
        Code = code.Trim().ToUpperInvariant();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>Fakultetni va uning barcha yo'nalishlarini arxivlaydi (soft delete).
    /// DIQQAT: chaqiruvchi fakultetni <c>Include(f => f.Directions)</c> bilan yuklashi shart —
    /// aks holda faqat yuklangan yo'nalishlar arxivlanadi, qolganlari "osilib qoladi".</summary>
    public void Delete(DateTimeOffset now)
    {
        IsDeleted = true;
        DeletedAt = now;

        foreach (var direction in _directions)
        {
            direction.IsDeleted = true;
            direction.DeletedAt = now;
        }
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
