namespace Amaliyotchi.Application.Features.Admin.Groups;

/// <summary>Yaratish/tahrirlash/holat o'zgartirish amallarining yagona javob shakli.
/// <paramref name="AcademicYear"/> — guruh biriktirilgan o'quv yili nomi ("2026-2027").</summary>
public sealed record GroupDto(Guid Id, Guid DirectionId, string Name, int Course, bool IsActive, string AcademicYear);
