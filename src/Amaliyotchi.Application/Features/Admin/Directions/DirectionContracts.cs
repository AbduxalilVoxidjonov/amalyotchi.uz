namespace Amaliyotchi.Application.Features.Admin.Directions;

/// <summary>Yaratish/tahrirlash/holat o'zgartirish amallarining yagona javob shakli — breadcrumb uchun
/// kafedra va fakultet ham qo'shilgan.</summary>
public sealed record DirectionDto(
    Guid Id, Guid DepartmentId, string DepartmentName, Guid FacultyId, string FacultyName,
    string Name, string Code, bool IsActive);

/// <summary>Ro'yxat qatori: yo'nalish + bolalar soni (o'chirilmagan guruh/talaba).</summary>
public sealed record DirectionRow(Guid Id, string Name, string Code, bool IsActive, int Groups, int Students);
