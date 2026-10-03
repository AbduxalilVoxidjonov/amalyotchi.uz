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

    /// <summary>Ish vaqti uchun eng qisqa davomiylik (daqiqa).</summary>
    public const int MinWorkMinutes = 60;

    /// <summary>Talabaning o'zi belgilagan kelish vaqti (Toshkent). null — davr soatlari ishlatiladi.
    /// <see cref="WorkHoursEffectiveFrom"/> dan boshlab amal qiladi.</summary>
    public TimeOnly? WorkStart { get; private set; }

    /// <summary>Talabaning o'zi belgilagan ketish vaqti. <see cref="WorkStart"/> bilan birga null yoki birga qiymatli.</summary>
    public TimeOnly? WorkEnd { get; private set; }

    /// <summary><see cref="WorkStart"/>/<see cref="WorkEnd"/> shu sanadan (Toshkent) amal qiladi; undan oldin —
    /// <see cref="PreviousWorkStart"/>/<see cref="PreviousWorkEnd"/>. Hech qachon o'rnatilmagan bo'lsa null.</summary>
    public DateOnly? WorkHoursEffectiveFrom { get; private set; }

    /// <summary>O'zgarish kuchga kirgunga qadar amal qiladigan oldingi soatlar (null — davr soatlari).</summary>
    public TimeOnly? PreviousWorkStart { get; private set; }
    public TimeOnly? PreviousWorkEnd { get; private set; }

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

    /// <summary>Talaba o'z ish vaqtini belgilaydi (ikkalasi null — davr soatlariga qaytish). O'zgarish ERTADAN
    /// (<paramref name="today"/> + 1, Toshkent) kuchga kiradi — bugungi kelish vaqtini surib "kech keldi"dan qochib
    /// bo'lmaydi. Joriy soatlar allaqachon amalda bo'lsa ular "oldingi"ga o'tadi; hali kuchga kirmagan o'zgarish
    /// bo'lsa — faqat u almashtiriladi (oldingi soatlar saqlanadi).</summary>
    public void SetWorkHours(TimeOnly? start, TimeOnly? end, DateOnly today)
    {
        if (start is null != end is null)
            throw new DomainException("Kelish va ketish vaqtlari birga ko'rsatilishi kerak.");
        if (start is { } s && end is { } e)
        {
            if (e <= s)
                throw new DomainException("Ketish vaqti kelish vaqtidan keyin bo'lishi kerak.");
            if ((e.ToTimeSpan() - s.ToTimeSpan()).TotalMinutes < MinWorkMinutes)
                throw new DomainException("Ish vaqti kamida 1 soat bo'lishi kerak.");
        }

        if (WorkHoursEffectiveFrom is null || WorkHoursEffectiveFrom <= today)
        {
            PreviousWorkStart = WorkStart;
            PreviousWorkEnd = WorkEnd;
        }

        WorkStart = start;
        WorkEnd = end;
        WorkHoursEffectiveFrom = today.AddDays(1);
    }

    /// <summary><paramref name="date"/> kuni amaldagi o'z soatlari; null — davr (standart) soatlari.</summary>
    public (TimeOnly Start, TimeOnly End)? HoursOn(DateOnly date)
        => ResolveHours(WorkStart, WorkEnd, WorkHoursEffectiveFrom, PreviousWorkStart, PreviousWorkEnd, date);

    /// <summary><see cref="HoursOn"/> ning statik shakli — so'rov proyeksiyalaridan olingan ustunlar uchun.</summary>
    public static (TimeOnly Start, TimeOnly End)? ResolveHours(
        TimeOnly? workStart, TimeOnly? workEnd, DateOnly? effectiveFrom,
        TimeOnly? previousStart, TimeOnly? previousEnd, DateOnly date)
    {
        var current = effectiveFrom is null || date >= effectiveFrom;
        var (start, end) = current ? (workStart, workEnd) : (previousStart, previousEnd);
        return start is { } s && end is { } e ? (s, e) : null;
    }

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
