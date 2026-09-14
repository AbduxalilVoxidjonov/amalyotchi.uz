using Amaliyotchi.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Auth;

/// <summary>Auth handler'lari uchun umumiy yuklash: foydalanuvchi + talaba profili + guruh
/// (<see cref="UserSummaryDto.From"/> to'liq to'ldirilishi uchun).</summary>
public static class UserQueries
{
    public static IQueryable<User> WithSummary(this IQueryable<User> users)
        => users
            .Include(u => u.StudentProfile!)
            .ThenInclude(p => p.Group);

    /// <summary>Kuzatilmaydigan proyeksiya — <c>GET /me</c> kabi faqat o'qish uchun.</summary>
    public static IQueryable<UserSummaryDto> SelectSummary(this IQueryable<User> users)
        => users.Select(u => new UserSummaryDto(
            u.Id,
            u.FullName,
            u.Role,
            u.FacultyId,
            u.PhoneNumber,
            u.StudentProfile != null ? u.StudentProfile.StudentGroupId : null,
            u.StudentProfile != null ? u.StudentProfile.Group.Name : null,
            u.StudentProfile != null ? u.StudentProfile.Group.Course : null,
            u.StudentProfile != null ? u.StudentProfile.HemisId : null));
}
