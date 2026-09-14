using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Settings;

/// <summary>Bayram — davomat maxrajiga kirmaydi. <see cref="IsRecurring"/> bo'lsa har yili shu kun/oy.</summary>
public sealed class Holiday : AuditableEntity, ISoftDeletable
{
    public const int NameMaxLength = 200;

    private Holiday() { }

    public DateOnly Date { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsRecurring { get; private set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public static Holiday Create(DateOnly date, string name, bool isRecurring)
    {
        var holiday = new Holiday { Date = date, IsRecurring = isRecurring };
        holiday.Rename(name);
        return holiday;
    }

    public void Rename(string name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException("Bayram nomi bo'sh bo'lishi mumkin emas.");
        if (trimmed.Length > NameMaxLength)
            throw new DomainException($"Bayram nomi {NameMaxLength} belgidan oshmasligi kerak.");
        Name = trimmed;
    }

    public void Reschedule(DateOnly date, bool isRecurring)
    {
        Date = date;
        IsRecurring = isRecurring;
    }

    /// <summary>Berilgan kun shu bayramga to'g'ri keladimi (takrorlanuvchi — yildan qat'i nazar).</summary>
    public bool AppliesTo(DateOnly date)
        => IsRecurring
            ? date.Month == Date.Month && date.Day == Date.Day
            : date == Date;
}
