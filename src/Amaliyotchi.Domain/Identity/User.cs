using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Students;
using Hemis = Amaliyotchi.Domain.ValueObjects.HemisId;
using Phone = Amaliyotchi.Domain.ValueObjects.PhoneNumber;

namespace Amaliyotchi.Domain.Identity;

/// <summary>Tizimning har qanday foydalanuvchisi: admin, tyutor yoki talaba.
/// Admin va tyutor HEMIS ID + parol bilan, talaba Telegram orqali kiradi.</summary>
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

    /// <summary>Xodim (admin/tyutor) uchun login identifikatori — HEMIS ID. Faqat parol bilan
    /// yaratilgan hisoblarda to'ldiriladi; talabada null (uning HEMIS ID'si <see cref="Students.StudentProfile.HemisId"/>
    /// da, login sifatida ishlatilmaydi — talaba Telegram orqali kiradi).</summary>
    public string? HemisId { get; private set; }

    /// <summary>Talaba uchun — Telegram hisobi. Admin/tyutorda bo'lmasligi mumkin.</summary>
    public long? TelegramUserId { get; private set; }

    /// <summary>Tyutor va talabaning ma'lumot ko'lami shu maydonga tayanadi.
    /// Admin uchun null — u barcha fakultetni ko'radi.</summary>
    public Guid? FacultyId { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    /// <summary>Talaba uchun akademik profil (1:1, FK <c>StudentProfile.UserId</c>). Admin/tyutorda null.</summary>
    public StudentProfile? StudentProfile { get; private set; }

    public bool IsStudent => Role == UserRole.Student;

    /// <summary>Parol bilan kiradigan foydalanuvchi (admin yoki tyutor). Login identifikatori — HEMIS ID
    /// (<paramref name="hemisId"/>), majburiy. Telefon raqami faqat aloqa uchun — ixtiyoriy.</summary>
    public static User CreateWithPassword(
        string fullName, string hemisId, string? phoneNumber, string passwordHash, UserRole role, Guid? facultyId = null)
    {
        if (role == UserRole.Student)
            throw new DomainException("Talaba parol bilan yaratilmaydi — u Telegram orqali kiradi.");
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Parol xeshi bo'sh bo'lishi mumkin emas.");
        if (role == UserRole.Tutor && facultyId is null)
            throw new DomainException("Tyutor fakultetga biriktirilishi shart.");

        return new User(fullName, role)
        {
            HemisId = Hemis.Normalize(hemisId),
            PhoneNumber = phoneNumber is null ? null : Phone.Normalize(phoneNumber),
            PasswordHash = passwordHash,
            FacultyId = facultyId
        };
    }

    /// <summary>Talaba hisobi. Tyutor oldindan yaratadi, talaba keyin Telegram bilan bog'laydi.</summary>
    public static User CreateStudent(string fullName, Guid facultyId, string? phoneNumber = null)
        => new(fullName, UserRole.Student)
        {
            FacultyId = facultyId,
            PhoneNumber = phoneNumber is null ? null : Phone.Normalize(phoneNumber)
        };

    public void LinkTelegram(long telegramUserId, string phoneNumber)
    {
        if (Role != UserRole.Student)
            throw new DomainException("Telegram hisobi faqat talabaga bog'lanadi.");
        if (TelegramUserId is not null && TelegramUserId != telegramUserId)
            throw new ConflictException("Bu hisobga boshqa Telegram akkaunti bog'langan.");

        TelegramUserId = telegramUserId;
        PhoneNumber = Phone.Normalize(phoneNumber);
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (Role == UserRole.Student)
            throw new DomainException("Talabaga parol o'rnatilmaydi.");
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Parol xeshi bo'sh bo'lishi mumkin emas.");

        PasswordHash = passwordHash;
    }

    public void Rename(string fullName) => FullName = Normalize(fullName);

    public void AssignToFaculty(Guid facultyId) => FacultyId = facultyId;

    public void MarkLogin(DateTimeOffset at) => LastLoginAt = at;

    /// <summary>Hisobni faolsizlantiradi va uning barcha refresh tokenlarini bekor qiladi.
    /// DIQQAT: faqat XOTIRADAGI tokenlar bekor qilinadi — chaqiruvchi foydalanuvchini
    /// <c>Include(u => u.RefreshTokens)</c> bilan yuklashi shart, aks holda bazadagi
    /// tokenlar faol qoladi va foydalanuvchi sessiyasini yangilashda davom etadi.</summary>
    public void Deactivate()
    {
        IsActive = false;
        foreach (var token in _refreshTokens)
            token.Revoke(DateTimeOffset.UtcNow, "Foydalanuvchi faolsizlantirildi");
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
