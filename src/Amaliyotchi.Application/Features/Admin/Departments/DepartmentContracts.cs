namespace Amaliyotchi.Application.Features.Admin.Departments;

/// <summary>Yaratish/tahrirlash/holat o'zgartirish amallarining yagona javob shakli.</summary>
public sealed record DepartmentDto(Guid Id, Guid FacultyId, string FacultyName, string Name, string Code, bool IsActive);

/// <summary>Ro'yxat qatori: kafedra + bolalar soni (o'chirilmagan yo'nalish/guruh/talaba).</summary>
public sealed record DepartmentRow(Guid Id, string Name, string Code, bool IsActive, int Directions, int Groups, int Students);
