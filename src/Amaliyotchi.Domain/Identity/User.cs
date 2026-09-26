using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Students;
using Hemis = Amaliyotchi.Domain.ValueObjects.HemisId;
using Phone = Amaliyotchi.Domain.ValueObjects.PhoneNumber;

namespace Amaliyotchi.Domain.Identity;

/// <summary>Tizimning har qanday foydalanuvchisi: admin, tyutor yoki talaba.
/// Admin va tyutor HEMIS ID + parol bilan kiradi; talaba — Telegram orqali yoki (xodim parol o'rnatgan bo'lsa)
/// <see cref="Students.StudentProfile.HemisId"/> + parol bilan (oddiy brauzer).</summary>
public sealed class User : AuditableEntity, ISoftDeletable
{
    private readonly List<RefreshToken> _refreshTokens = [];
    private readonly List<TutorFaculty> _faculties = [];

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

    /// <summary>Parolni xodim (admin/tyutor) o'rnatgan — foydalanuvchi keyingi kirishda o'z parolini o'rnatishi kerak.
    /// <see cref="SetTemporaryPassword"/> true qiladi, <see cref="ChangeOwnPassword"/> false qiladi.</summary>
    public bool MustChangePassword { get; private set; }

    /// <summary>Xodim (admin/tyutor) uchun login identifikatori — HEMIS ID. Faqat parol bilan
    /// yaratilgan hisoblarda to'ldiriladi; talabada null (uning HEMIS ID'si <see cref="Students.StudentProfile.HemisId"/>
    /// da — talaba parol bilan kirganda login shu profil maydoni bo'yicha topiladi).</summary>
    public string? HemisId { get; private set; }

    /// <summary>Talaba uchun — Telegram hisobi. Admin/tyutorda bo'lmasligi mumkin.</summary>
    public long? TelegramUserId { get; private set; }

    /// <summary>Talabaning fakulteti; tyutor uchun — ASOSIY fakultet (<see cref="Faculties"/> ro'yxatining birinchisi,
    /// auth/JWT mosligi uchun). Admin uchun null — u barcha fakultetni ko'radi. Tyutorning to'liq fakultetlar to'plami —
    /// <see cref="Faculties"/>; admin-tyutor mantig'i o'shanga tayanadi.</summary>
    public Guid? FacultyId { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    /// <summary>Tyutor biriktirilgan fakultetlar (kamida bittasi). Admin/talabada bo'sh. Faqat <see cref="SetFaculties"/>
    /// orqali o'zgaradi; chaqiruvchi <c>Include(u => u.Faculties)</c> bilan yuklashi shart — aks holda olib tashlash
    /// bazaga yetib bormaydi.</summary>
    public IReadOnlyCollection<TutorFaculty> Faculties => _faculties.AsReadOnly();

    /// <summary>Talaba uchun akademik profil (1:1, FK <c>StudentProfile.UserId</c>). Admin/tyutorda null.</summary>
    public StudentProfile? StudentProfile { get; private set; }

    public bool IsStudent => Role == UserRole.Student;

    /// <summary>Parol bilan kiradigan foydalanuvchi (admin yoki tyutor). Login identifikatori — HEMIS ID
    /// (<paramref name="hemisId"/>), majburiy. Telefon raqami faqat aloqa uchun — ixtiyoriy. Tyutor uchun
    /// <paramref name="facultyId"/> — yagona fakultet (ko'p fakultet: boshqa overload yoki <see cref="SetFaculties"/>).</summary>
    public static User CreateWithPassword(
        string fullName, string hemisId, string? phoneNumber, string passwordHash, UserRole role, Guid? facultyId = null)
        => CreateWithPassword(fullName, hemisId, phoneNumber, passwordHash, role, facultyId is { } id ? [id] : []);

    /// <summary>Parol bilan kiradigan foydalanuvchi; tyutor uchun <paramref name="facultyIds"/> — biriktiriladigan
    /// fakultetlar (kamida bittasi, birinchisi asosiy — <see cref="FacultyId"/>). Admin uchun ro'yxat bo'sh bo'lishi kerak.</summary>
    public static User CreateWithPassword(
        string fullName, string hemisId, string? phoneNumber, string passwordHash, UserRole role, IReadOnlyCollection<Guid> facultyIds)
    {
        if (role == UserRole.Student)
            throw new DomainException("Talaba parol bilan yaratilmaydi — u Telegram orqali kiradi.");
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Parol xeshi bo'sh bo'lishi mumkin emas.");
        if (role == UserRole.Tutor && facultyIds.Count == 0)
            throw new DomainException(FacultyRequiredMessage);
        if (role == UserRole.Admin && facultyIds.Count > 0)
            throw new DomainException("Admin fakultetga biriktirilmaydi — u barcha fakultetni ko'radi.");

        var user = new User(fullName, role)
        {
            HemisId = Hemis.Normalize(hemisId),
            PhoneNumber = phoneNumber is null ? null : Phone.Normalize(phoneNumber),
            PasswordHash = passwordHash
        };
        if (role == UserRole.Tutor)
            user.SetFaculties(facultyIds);

        return user;
    }

    /// <summary>Talaba hisobi. Tyutor oldindan yaratadi, talaba keyin Telegram bilan bog'laydi.</summary>
    public static User CreateStudent(string fullName, Guid facultyId, string? phoneNumber = null)
        => new(fullName, UserRole.Student)
        {
            FacultyId = facultyId,
            PhoneNumber = phoneNumber is null ? null : Phone.Normalize(phoneNumber)
        };

    /// <summary>Talabaga Telegram hisobini bog'laydi. <paramref name="phoneNumber"/> berilsa telefon ham
    /// yangilanadi; berilmasa (<c>null</c> — masalan Mini App <c>initData</c> da telefon yo'q) mavjud telefon
    /// o'zgarmay qoladi. Shu id bilan qayta bog'lash — xato emas (idempotent).</summary>
    public void LinkTelegram(long telegramUserId, string? phoneNumber = null)
    {
        if (Role != UserRole.Student)
            throw new DomainException("Telegram hisobi faqat talabaga bog'lanadi.");
        if (TelegramUserId is not null && TelegramUserId != telegramUserId)
            throw new ConflictException("Bu hisobga boshqa Telegram akkaunti bog'langan.");

        TelegramUserId = telegramUserId;
        if (phoneNumber is not null)
            PhoneNumber = Phone.Normalize(phoneNumber);
    }

    /// <summary>Parol xeshini almashtiradi (<see cref="MustChangePassword"/> ga tegmaydi) — xeshni yangilash (rehash),
    /// tyutor parolini tiklash va seed uchun.</summary>
    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Parol xeshi bo'sh bo'lishi mumkin emas.");

        PasswordHash = passwordHash;
    }

    /// <summary>Xodim (admin/tyutor) talabaga vaqtinchalik parol o'rnatadi: talaba birinchi kirishda
    /// o'z parolini o'rnatishi kerak (<see cref="MustChangePassword"/> = true).</summary>
    public void SetTemporaryPassword(string passwordHash)
    {
        if (Role != UserRole.Student)
            throw new DomainException("Vaqtinchalik parol faqat talabaga o'rnatiladi.");

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

    /// <summary>Xodim (admin/tyutor) login identifikatorini — HEMIS ID — almashtiradi (normallashtirilgan holda).
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

    /// <summary>Talabaning fakultetini o'zgartiradi (guruh ko'chirilganda). Tyutor uchun — <see cref="SetFaculties"/>.</summary>
    public void AssignToFaculty(Guid facultyId)
    {
        if (Role != UserRole.Student)
            throw new DomainException("Tyutor fakultetlari SetFaculties orqali o'zgartiriladi.");
        FacultyId = facultyId;
    }

    public const string FacultyRequiredMessage = "Tyutor kamida bitta fakultetga biriktirilishi shart.";

    /// <summary>Tyutorning fakultetlar to'plamini ALMASHTIRADI: ro'yxatda yo'qlari olib tashlanadi, yangilari
    /// qo'shiladi (mavjudlari saqlanadi — yozuv Id'si o'zgarmaydi), takrorlar bittaga keltiriladi; ro'yxatning
    /// birinchisi <see cref="FacultyId"/> (asosiy fakultet) bo'ladi. Faqat tyutor uchun; bo'sh ro'yxat → xato.
    /// Chaqiruvchi <see cref="Faculties"/> ni yuklagan bo'lishi shart (<c>Include</c>).</summary>
    public void SetFaculties(IReadOnlyCollection<Guid> facultyIds)
    {
        if (Role != UserRole.Tutor)
            throw new DomainException("Fakultetlar to'plami faqat tyutorga biriktiriladi.");
        if (facultyIds.Any(id => id == Guid.Empty))
            throw new DomainException("Fakultet ko'rsatilmagan.");

        var wanted = facultyIds.Distinct().ToList();
        if (wanted.Count == 0)
            throw new DomainException(FacultyRequiredMessage);

        _faculties.RemoveAll(f => !wanted.Contains(f.FacultyId));
        foreach (var facultyId in wanted)
        {
            if (_faculties.All(f => f.FacultyId != facultyId))
                _faculties.Add(TutorFaculty.Create(Id, facultyId));
        }

        FacultyId = wanted[0];
    }

    public void MarkLogin(DateTimeOffset at) => LastLoginAt = at;

    /// <summary>Hisobni faolsizlantiradi va uning barcha refresh tokenlarini bekor qiladi.
    /// DIQQAT: faqat XOTIRADAGI tokenlar bekor qilinadi — chaqiruvchi foydalanuvchini
    /// <c>Include(u => u.RefreshTokens)</c> bilan yuklashi shart, aks holda bazadagi
    /// tokenlar faol qoladi va foydalanuvchi sessiyasini yangilashda davom etadi.</summary>
    public void Deactivate()
    {
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
