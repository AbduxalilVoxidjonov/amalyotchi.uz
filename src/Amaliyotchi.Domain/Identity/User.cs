using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Students;
using Hemis = Amaliyotchi.Domain.ValueObjects.HemisId;
using Phone = Amaliyotchi.Domain.ValueObjects.PhoneNumber;

namespace Amaliyotchi.Domain.Identity;

/// <summary>Tizimning har qanday foydalanuvchisi: admin yoki talaba.
/// Admin login (HEMIS ID formatida) + parol bilan kiradi; talaba — Telegram orqali yoki (xodim parol o'rnatgan bo'lsa)
/// <see cref="Students.StudentProfile.HemisId"/> + parol bilan (oddiy brauzer).</summary>
public sealed class User : AuditableEntity, ISoftDeletable
{
    private readonly List<RefreshToken> _refreshTokens = [];

    private User() { }

    private User(string fullName, UserRole role)
    {
        FullName = Normalize(fullName);
        Role = role;
        IsActive = true;
    }

    public string FullName { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? PasswordHash { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>Bosh admin — boshqa adminlarni boshqaradi (<c>/api/admin/admins</c>). Tizimda kamida bitta faol bosh
    /// admin bo'lishi kerak; bosh admin bloklanmaydi va o'chirilmaydi. Faqat admin uchun.</summary>
    public bool IsSuperAdmin { get; private set; }

    /// <summary>Parolni xodim (admin) o'rnatgan — foydalanuvchi keyingi kirishda o'z parolini o'rnatishi kerak.
    /// <see cref="SetTemporaryPassword"/> true qiladi, <see cref="ChangeOwnPassword"/> false qiladi.</summary>
    public bool MustChangePassword { get; private set; }

    /// <summary>Admin uchun login identifikatori — HEMIS ID. Faqat parol bilan
    /// yaratilgan hisoblarda to'ldiriladi; talabada null (uning HEMIS ID'si <see cref="Students.StudentProfile.HemisId"/>
    /// da — talaba parol bilan kirganda login shu profil maydoni bo'yicha topiladi).</summary>
    public string? HemisId { get; private set; }

    /// <summary>Talaba uchun — Telegram hisobi. Adminda bo'lmasligi mumkin.</summary>
    public long? TelegramUserId { get; private set; }

    /// <summary>Telegram hisobi qachon bog'langan (<see cref="LinkTelegram"/>). Shu maydon qo'shilishidan oldin
    /// bog'langan hisoblarda null bo'lishi mumkin (migratsiya audit jurnalidan tiklaydi).</summary>
    public DateTimeOffset? TelegramLinkedAt { get; private set; }

    /// <summary>Bot bu talabaga yoza olmasligi aniqlangan vaqt (Telegram 403: botni bloklagan / ishga tushirmagan,
    /// 400 chat not found). Muvaffaqiyatli yuborish yoki <c>allows_write_to_pm=true</c> bilan Telegram kirishi tozalaydi.</summary>
    public DateTimeOffset? TelegramBotBlockedAt { get; private set; }

    /// <summary>Talabaning fakulteti. Admin uchun null — u barcha fakultetni ko'radi.</summary>
    public Guid? FacultyId { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    /// <summary>Talaba uchun akademik profil (1:1, FK <c>StudentProfile.UserId</c>). Adminda null.</summary>
    public StudentProfile? StudentProfile { get; private set; }

    public bool IsStudent => Role == UserRole.Student;

    /// <summary>Parol bilan kiradigan foydalanuvchi — admin. Login identifikatori — HEMIS ID formatidagi raqamlar
    /// (<paramref name="hemisId"/>), majburiy. Telefon raqami faqat aloqa uchun — ixtiyoriy.</summary>
    public static User CreateWithPassword(
        string fullName, string hemisId, string? phoneNumber, string passwordHash, UserRole role)
    {
        if (role != UserRole.Admin)
            throw new DomainException("Parol bilan faqat admin yaratiladi — talaba Telegram orqali kiradi.");
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Parol xeshi bo'sh bo'lishi mumkin emas.");

        return new User(fullName, role)
        {
            HemisId = Hemis.Normalize(hemisId),
            PhoneNumber = phoneNumber is null ? null : Phone.Normalize(phoneNumber),
            PasswordHash = passwordHash
        };
    }

    /// <summary>Talaba hisobi. Admin oldindan yaratadi, talaba keyin Telegram bilan bog'laydi.</summary>
    public static User CreateStudent(string fullName, Guid facultyId, string? phoneNumber = null)
        => new(fullName, UserRole.Student)
        {
            FacultyId = facultyId,
            PhoneNumber = phoneNumber is null ? null : Phone.Normalize(phoneNumber)
        };

    /// <summary>Talabaga Telegram hisobini bog'laydi. <paramref name="phoneNumber"/> berilsa telefon ham
    /// yangilanadi; berilmasa (<c>null</c> — masalan Mini App <c>initData</c> da telefon yo'q) mavjud telefon
    /// o'zgarmay qoladi. Shu id bilan qayta bog'lash — xato emas (idempotent). <see cref="TelegramLinkedAt"/> —
    /// birinchi bog'lash vaqti (<paramref name="linkedAt"/>, berilmasa joriy UTC); qayta bog'lashda o'zgarmaydi.</summary>
    public void LinkTelegram(long telegramUserId, string? phoneNumber = null, DateTimeOffset? linkedAt = null)
    {
        if (Role != UserRole.Student)
            throw new DomainException("Telegram hisobi faqat talabaga bog'lanadi.");
        if (TelegramUserId is not null && TelegramUserId != telegramUserId)
            throw new ConflictException("Bu hisobga boshqa Telegram akkaunti bog'langan.");

        TelegramUserId = telegramUserId;
        TelegramLinkedAt ??= linkedAt ?? DateTimeOffset.UtcNow;
        if (phoneNumber is not null)
            PhoneNumber = Phone.Normalize(phoneNumber);
    }

    /// <summary>Parol xeshini almashtiradi (<see cref="MustChangePassword"/> ga tegmaydi) — xeshni yangilash (rehash),
    /// seed uchun.</summary>
    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Parol xeshi bo'sh bo'lishi mumkin emas.");

        PasswordHash = passwordHash;
    }

    /// <summary>Admin vaqtinchalik parol o'rnatadi (talabaga yoki — bosh admin — boshqa adminga): foydalanuvchi
    /// birinchi kirishda o'z parolini o'rnatishi kerak (<see cref="MustChangePassword"/> = true).</summary>
    public void SetTemporaryPassword(string passwordHash)
    {
        SetPasswordHash(passwordHash);
        MustChangePassword = true;
    }

    /// <summary>Foydalanuvchi parolini o'zi o'zgartirdi — majburiy almashtirish talabi olib tashlanadi.</summary>
    public void ChangeOwnPassword(string passwordHash)
    {
        SetPasswordHash(passwordHash);
        MustChangePassword = false;
    }

    public void Rename(string fullName) => FullName = Normalize(fullName);

    /// <summary>Admin login identifikatorini — HEMIS ID — almashtiradi (normallashtirilgan holda).
    /// Talabaning logini <see cref="Students.StudentProfile.HemisId"/> da — bu yerdan o'zgartirilmaydi. Joriy login
    /// bilan bir xil qiymat → xato. Band emasligini (boshqa foydalanuvchi/talaba profili) chaqiruvchi tekshiradi.</summary>
    public void ChangeHemisId(string hemisId)
    {
        if (Role == UserRole.Student)
            throw new DomainException("Talabaning HEMIS ID'si profil orqali o'zgartiriladi.");

        var normalized = Hemis.Normalize(hemisId);
        if (normalized == HemisId)
            throw new DomainException(SameHemisIdMessage);

        HemisId = normalized;
    }

    public const string SameHemisIdMessage = "Yangi login joriy logindan farq qilishi kerak.";

    /// <summary>Aloqa telefonini o'zgartiradi (bo'sh → null). Talabaning telefoni Telegram orqali
    /// bog'lanadi (<see cref="LinkTelegram"/>) — bu yerdan o'zgartirilmaydi.</summary>
    public void ChangePhoneNumber(string? phoneNumber)
    {
        if (Role == UserRole.Student)
            throw new DomainException("Talabaning telefoni Telegram orqali bog'lanadi.");

        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : Phone.Normalize(phoneNumber);
    }

    /// <summary>Talabaning fakultetini o'zgartiradi (guruh ko'chirilganda).</summary>
    public void AssignToFaculty(Guid facultyId)
    {
        if (Role != UserRole.Student)
            throw new DomainException("Fakultet faqat talabaga biriktiriladi.");
        FacultyId = facultyId;
    }

    public const string SuperAdminBlockMessage = "Bosh adminni bloklab bo'lmaydi.";
    public const string SelfBlockMessage = "O'zingizni bloklay olmaysiz.";

    /// <summary>Adminni bosh admin qiladi (seed / migratsiya ekvivalenti). Faqat faol admin uchun.</summary>
    public void MakeSuperAdmin()
    {
        if (Role != UserRole.Admin)
            throw new DomainException("Bosh admin faqat admin bo'lishi mumkin.");
        if (!IsActive)
            throw new DomainException("Faol bo'lmagan admin bosh admin bo'la olmaydi.");
        IsSuperAdmin = true;
    }

    public void MarkLogin(DateTimeOffset at) => LastLoginAt = at;

    /// <summary>Bot talabaga yoza olmadi (bloklangan / ishga tushirilmagan). Allaqachon belgilangan bo'lsa — birinchi vaqt saqlanadi.</summary>
    public void MarkBotBlocked(DateTimeOffset at) => TelegramBotBlockedAt ??= at;

    /// <summary>Bot yana yoza oladi (muvaffaqiyatli yuborish yoki Telegram kirishida <c>allows_write_to_pm=true</c>).</summary>
    public void ClearBotBlocked() => TelegramBotBlockedAt = null;

    /// <summary>Hisobni faolsizlantiradi va uning barcha refresh tokenlarini bekor qiladi.
    /// DIQQAT: faqat XOTIRADAGI tokenlar bekor qilinadi — chaqiruvchi foydalanuvchini
    /// <c>Include(u => u.RefreshTokens)</c> bilan yuklashi shart, aks holda bazadagi
    /// tokenlar faol qoladi va foydalanuvchi sessiyasini yangilashda davom etadi.</summary>
    public void Deactivate()
    {
        if (IsSuperAdmin)
            throw new ConflictException(SuperAdminBlockMessage);

        IsActive = false;
        RevokeRefreshTokens(DateTimeOffset.UtcNow, "Foydalanuvchi faolsizlantirildi");
    }

    /// <summary>Barcha yuklangan refresh tokenlarni bekor qiladi (parol tiklanganda — eski sessiyalar
    /// yashamasin). <see cref="Deactivate"/> kabi faqat XOTIRADAGI tokenlarga ta'sir qiladi — chaqiruvchi
    /// <c>Include(u => u.RefreshTokens)</c> bilan yuklashi shart.</summary>
    public void RevokeRefreshTokens(DateTimeOffset now, string reason)
    {
        foreach (var token in _refreshTokens)
            token.Revoke(now, reason);
    }

    /// <summary>Muddati o'tgan refresh tokenlarni to'plamdan olib tashlaydi (EF ularni o'chiradi).
    /// Bekor qilingan, lekin muddati o'tmagan tokenlar saqlanadi — rotatsiya zanjiri
    /// (<c>ReplacedByToken</c>) uzilmasligi uchun. Faqat yuklangan tokenlarga ta'sir qiladi.</summary>
    public void PruneRefreshTokens(DateTimeOffset now)
        => _refreshTokens.RemoveAll(t => t.ExpiresAt <= now);

    public void Activate() => IsActive = true;

    public RefreshToken IssueRefreshToken(string token, DateTimeOffset expiresAt, string? createdByIp)
    {
        if (!IsActive)
            throw new ForbiddenException("Hisob faol emas.");

        var refreshToken = RefreshToken.Issue(Id, token, expiresAt, createdByIp);
        _refreshTokens.Add(refreshToken);
        return refreshToken;
    }

    private static string Normalize(string value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException("FISH bo'sh bo'lishi mumkin emas.");
        return trimmed;
    }

}
