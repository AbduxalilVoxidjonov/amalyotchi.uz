using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Organization;

public sealed class Faculty : AuditableEntity, ISoftDeletable
{
    private readonly List<Department> _departments = [];

    private Faculty() { }

    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;

    /// <summary>Faol emas fakultet ro'yxatda qoladi (o'chirilmagan), lekin faoliyat ko'rsatmaydi deb
    /// belgilanadi — masalan yopilgan yoki vaqtincha to'xtatilgan. Soft-delete bilan aralashtirilmasin.</summary>
    public bool IsActive { get; private set; } = true;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public IReadOnlyCollection<Department> Departments => _departments.AsReadOnly();

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

    /// <summary>Faqat o'zini arxivlaydi (soft delete) — kafedralarini kaskad qilmaydi. Chaqiruvchi
    /// oldindan o'chirilmagan kafedra yo'qligini tekshirishi shart (409 qoidasi Application qatlamida).</summary>
    public void Delete(DateTimeOffset now)
    {
        IsDeleted = true;
        DeletedAt = now;
    }

    public Department AddDepartment(string name, string code)
    {
        if (_departments.Any(d => string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException($"'{code}' kodli kafedra bu fakultetda allaqachon mavjud.");

        var department = Department.Create(Id, name, code);
        _departments.Add(department);
        return department;
    }
}
