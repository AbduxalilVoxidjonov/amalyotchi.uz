using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.ValueObjects;
using Phone = Amaliyotchi.Domain.ValueObjects.PhoneNumber;

namespace Amaliyotchi.Domain.Companies;

/// <summary>Amaliyot o'tkaziladigan korxona: STIR, manzil, geografik nuqta va geofence radiusi.
/// "Katta radius" / "shubhali" bayroqlari saqlanmaydi — ular Application qatlamida
/// chegaralar bo'yicha hisoblanadi (radius &gt; 500 m, talaba soni va h.k.).</summary>
public sealed class Company : AuditableEntity, ISoftDeletable
{
    public const int MinRadiusM = 50;
    public const int MaxRadiusM = 1000;
    /// <summary>Sozlama (<c>geofenceRadius</c>) bo'lmaganda ishlatiladigan standart radius.</summary>
    public const int DefaultRadiusM = 200;
    public const int NameMaxLength = 200;
    public const int ActivityMaxLength = 200;
    public const int AddressMaxLength = 500;

    private Company() { }

    public string Name { get; private set; } = string.Empty;

    /// <summary>STIR (INN) — 9 raqam, faol korxonalar orasida takrorlanmaydi.</summary>
    public string Tin { get; private set; } = string.Empty;

    /// <summary>Faoliyat turi ("IT xizmatlari", "Qurilish").</summary>
    public string Activity { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;

    /// <summary>Korxonaning geografik nuqtasi. Infrastructure PostGIS geography'ga map qiladi.</summary>
    public GeoPoint Location { get; private set; }

    /// <summary>Geofence radiusi (m). Tyutor arizani tasdiqlashda o'zgartira oladi.</summary>
    public int RadiusM { get; private set; }

    /// <summary>Korxona tomonidan amaliyot rahbari.</summary>
    public string SupervisorName { get; private set; } = string.Empty;
    public string SupervisorPhone { get; private set; } = string.Empty;

    /// <summary>Mentor (ixtiyoriy) — kundalik ishni kuzatuvchi xodim.</summary>
    public string? MentorName { get; private set; }
    public string? MentorPhone { get; private set; }

    /// <summary>O'chirish o'rniga faolsizlantiriladi — eski arizalar/davomat bog'liqligi saqlanadi.</summary>
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public static Company Create(
        string name,
        string tin,
        string activity,
        string address,
        GeoPoint location,
        int radiusM,
        string supervisorName,
        string supervisorPhone,
        string? mentorName = null,
        string? mentorPhone = null)
    {
        var company = new Company { Location = location, IsActive = true };
        company.Tin = ValueObjects.Tin.Normalize(tin);
        company.SetRadius(radiusM);
        company.Update(name, activity, address, supervisorName, supervisorPhone, mentorName, mentorPhone);
        return company;
    }

    public void Update(
        string name,
        string activity,
        string address,
        string supervisorName,
        string supervisorPhone,
        string? mentorName = null,
        string? mentorPhone = null)
    {
        Name = Required(name, "Korxona nomi", NameMaxLength);
        Activity = Required(activity, "Faoliyat turi", ActivityMaxLength);
        Address = Required(address, "Manzil", AddressMaxLength);
        SupervisorName = Required(supervisorName, "Rahbar FISH", NameMaxLength);
        SupervisorPhone = Phone.Normalize(supervisorPhone);
        MentorName = string.IsNullOrWhiteSpace(mentorName) ? null : mentorName.Trim();
        MentorPhone = string.IsNullOrWhiteSpace(mentorPhone) ? null : Phone.Normalize(mentorPhone);
    }

    public void ChangeTin(string tin) => Tin = ValueObjects.Tin.Normalize(tin);

    /// <summary>Radiusni o'zgartiradi. Qiymat o'zgargan bo'lsa <c>true</c> — chaqiruvchi
    /// <c>AuditAction.RadiusChanged</c> yozishi uchun.</summary>
    public bool SetRadius(int radiusM)
    {
        if (radiusM is < MinRadiusM or > MaxRadiusM)
            throw new DomainException($"Radius {MinRadiusM}–{MaxRadiusM} m oralig'ida bo'lishi kerak.");

        if (RadiusM == radiusM)
            return false;

        RadiusM = radiusM;
        return true;
    }

    public void Relocate(double latitude, double longitude) => Location = new GeoPoint(latitude, longitude);

    public void Relocate(GeoPoint location) => Location = location;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    private static string Required(string? value, string label, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException($"{label} bo'sh bo'lishi mumkin emas.");
        if (trimmed.Length > maxLength)
            throw new DomainException($"{label} {maxLength} belgidan oshmasligi kerak.");
        return trimmed;
    }
}
