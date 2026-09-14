using Amaliyotchi.Domain.Enums;

namespace Amaliyotchi.Application.Common.Interfaces;

/// <summary>Joriy so'rovni kim yuborayotgani. Ma'lumot ko'lami (data scoping) va
/// audit yozuvi shu manbaga tayanadi.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    UserRole? Role { get; }

    /// <summary>Tyutor va talabaning ko'lami. Admin uchun null — cheklov yo'q.</summary>
    Guid? FacultyId { get; }

    bool IsAuthenticated { get; }
    string? IpAddress { get; }
    string? TraceId { get; }
}
