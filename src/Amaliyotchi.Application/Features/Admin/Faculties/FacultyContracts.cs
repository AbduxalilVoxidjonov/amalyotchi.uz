namespace Amaliyotchi.Application.Features.Admin.Faculties;

/// <summary>Yaratish/tahrirlash/holat o'zgartirish amallarining yagona javob shakli.</summary>
public sealed record FacultyDto(Guid Id, string Name, string Code, bool IsActive);
