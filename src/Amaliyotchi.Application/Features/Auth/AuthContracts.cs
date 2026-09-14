using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Identity;

namespace Amaliyotchi.Application.Features.Auth;

/// <summary>Kontrakt v2: talaba uchun guruh/kurs/HEMIS ID ham keladi (StudentProfile ⟕ StudentGroup);
/// admin va tyutorda bu maydonlar <c>null</c>.</summary>
public sealed record UserSummaryDto(
    Guid Id,
    string FullName,
    UserRole Role,
    Guid? FacultyId,
    string? PhoneNumber,
    Guid? GroupId,
    string? GroupName,
    int? Course,
    string? HemisId)
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
            profile?.HemisId);
    }
}

public sealed record AuthResultDto(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    UserSummaryDto User);
