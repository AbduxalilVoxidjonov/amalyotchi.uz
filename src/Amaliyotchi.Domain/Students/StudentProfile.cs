using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Domain.Organization;
using Amaliyotchi.Domain.ValueObjects;

namespace Amaliyotchi.Domain.Students;

/// <summary>Talabaning akademik profili: HEMIS ID, guruh, holat.
/// <see cref="User"/> — kimlik (kim), <see cref="StudentProfile"/> — akademik ma'lumot (qayerda o'qiydi).
/// Kurs alohida saqlanmaydi — u guruhdan (<see cref="StudentGroup.Course"/>) olinadi, ikkilanish bo'lmasin.</summary>
public sealed class StudentProfile : AuditableEntity, ISoftDeletable
{
    private StudentProfile() { }

    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;

    /// <summary>HEMIS tizimidagi talaba identifikatori. Faol profillar orasida takrorlanmaydi.</summary>
    public string HemisId { get; private set; } = string.Empty;

    public Guid StudentGroupId { get; private set; }
    public StudentGroup Group { get; private set; } = null!;

    public StudentStatus Status { get; private set; }

    /// <summary>Telegram orqali bog'lanish uchun bir martalik taklif tokeni (<c>/start INV_xxx</c>).
    /// Bog'langach tozalanadi.</summary>
    public string? InviteToken { get; private set; }
    public DateTimeOffset? InviteTokenExpiresAt { get; private set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public static StudentProfile Create(Guid userId, string hemisId, Guid studentGroupId)
    {
        if (userId == Guid.Empty)
            throw new DomainException("Talaba foydalanuvchisi ko'rsatilmagan.");
        if (studentGroupId == Guid.Empty)
            throw new DomainException("Talaba guruhga biriktirilishi shart.");

        return new StudentProfile
        {
            UserId = userId,
            HemisId = ValueObjects.HemisId.Normalize(hemisId),
            StudentGroupId = studentGroupId,
            Status = StudentStatus.Active
        };
    }

    /// <summary>Boshqa guruhga ko'chirish. Fakultet ham o'zgargan bo'lsa chaqiruvchi
    /// <see cref="User.AssignToFaculty"/> ni ham chaqirishi shart (§6.1 xavfi).</summary>
    public void MoveToGroup(Guid studentGroupId)
    {
        if (studentGroupId == Guid.Empty)
            throw new DomainException("Guruh ko'rsatilmagan.");
        StudentGroupId = studentGroupId;
    }

    public void ChangeHemisId(string hemisId) => HemisId = ValueObjects.HemisId.Normalize(hemisId);

    public void Suspend()
    {
        if (Status == StudentStatus.Graduated)
            throw new DomainException("Bitirgan talabani chetlashtirib bo'lmaydi.");
        Status = StudentStatus.Suspended;
    }

    public void Activate() => Status = StudentStatus.Active;

    public void Graduate() => Status = StudentStatus.Graduated;

    public bool IsActive => Status == StudentStatus.Active && !IsDeleted;

    public void IssueInviteToken(string token, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new DomainException("Taklif tokeni bo'sh bo'lishi mumkin emas.");

        InviteToken = token;
        InviteTokenExpiresAt = expiresAt;
    }

    /// <summary>Token haqiqiyligini tekshiradi va uni "ishlatilgan" deb tozalaydi.</summary>
    public void ConsumeInviteToken(string token, DateTimeOffset now)
    {
        if (InviteToken is null || !string.Equals(InviteToken, token, StringComparison.Ordinal))
            throw new DomainException("Taklif havolasi noto'g'ri.");
        if (InviteTokenExpiresAt is not null && InviteTokenExpiresAt <= now)
            throw new DomainException("Taklif havolasining muddati o'tgan — tyutordan yangisini oling.");

        InviteToken = null;
        InviteTokenExpiresAt = null;
    }
}
