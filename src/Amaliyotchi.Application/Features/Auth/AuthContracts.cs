using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Identity;

namespace Amaliyotchi.Application.Features.Auth;

/// <summary>Kontrakt v2: talaba uchun guruh/kurs ham keladi (StudentProfile ⟕ StudentGroup), adminda
/// bu maydonlar <c>null</c>. <c>HemisId</c> — talabada <c>StudentProfile.HemisId</c>, adminda
/// <c>User.HemisId</c> (ikkalasi ham login identifikatori sifatida ishlatiladi, shu DTO'da birlashtiriladi).
/// <c>IsSuperAdmin</c> — bosh admin (adminlarni boshqaradi); talabada doim <c>false</c>.</summary>
public sealed record UserSummaryDto(
    Guid Id,
    string FullName,
    UserRole Role,
    Guid? FacultyId,
    string? PhoneNumber,
    Guid? GroupId,
    string? GroupName,
    int? Course,
    string? HemisId,
    bool IsSuperAdmin)
{
    /// <summary>Login/Refresh/Me/Telegram bitta joydan quradi — maydon qo'shilsa faqat shu yer o'zgaradi.
    /// Talaba uchun chaqiruvchi <c>Include(u => u.StudentProfile).ThenInclude(p => p.Group)</c> bilan yuklashi shart
    /// (yuklanmagan bo'lsa guruh maydonlari null qaytadi).</summary>
    public static UserSummaryDto From(User user)
    {
        var profile = user.StudentProfile;
        return new UserSummaryDto(
            user.Id,
            user.FullName,
            user.Role,
            user.FacultyId,
            user.PhoneNumber,
            profile?.StudentGroupId,
            profile?.Group?.Name,
            profile?.Group?.Course,
            profile?.HemisId ?? user.HemisId,
            user.IsSuperAdmin);
    }
}

/// <param name="MustChangePassword">Parolni xodim o'rnatgan (vaqtinchalik) — mijoz foydalanuvchini avval
/// <c>POST /api/auth/change-password</c> ga yo'naltiradi. Telegram orqali kirishda ham qiymat qaytadi.</param>
public sealed record AuthResultDto(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    UserSummaryDto User,
    bool MustChangePassword);
