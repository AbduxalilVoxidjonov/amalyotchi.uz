using Amaliyotchi.Domain.Students;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

/// <summary>Tyutor biriktirilgan fakultet — ro'yxat va kartada (<c>faculties[]</c>, nom bo'yicha tartib).</summary>
public sealed record FacultyRef(Guid Id, string Code, string Name);

/// <summary>Tyutor kartasi — yaratish/tahrirlash/ko'lam biriktirish amallarining yagona javob shakli.
/// <paramref name="Faculties"/> — biriktirilgan fakultetlar (kamida bittasi, nom bo'yicha);
/// <paramref name="Scopes"/> — admin tanlagan faol ko'lamlar (fakultet/kafedra/yo'nalish/guruh);
/// <paramref name="Groups"/> — ulardan materializatsiya qilingan faol guruh biriktiruvlari.</summary>
public sealed record TutorDetail(
    Guid Id,
    string FullName,
    string HemisId,
    string? Phone,
    IReadOnlyList<FacultyRef> Faculties,
    bool IsActive,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<TutorScopeDto> Scopes,
    IReadOnlyList<TutorGroupDto> Groups);

/// <summary>Tyutorga faol biriktirilgan guruh. <paramref name="IsActive"/> — guruhning o'zi faolmi
/// (biriktiruv faol — aks holda ro'yxatga tushmaydi). <paramref name="Students"/> — guruhdagi talabalar soni.</summary>
public sealed record TutorGroupDto(
    Guid AssignmentId,
    Guid GroupId,
    string GroupName,
    int Course,
    string DirectionName,
    int Students,
    string AcademicYearName,
    bool IsActive);

/// <summary>Tyutorning faol ko'lami. <paramref name="Name"/> — tanlangan tugun nomi; <paramref name="Path"/> —
/// ota tugunlar <c>" › "</c> bilan (tugunning o'zisiz; fakultet darajasida <c>""</c>).
/// <paramref name="Groups"/>/<paramref name="Students"/> — qamrab olingan faol guruhlar va ulardagi talabalar.</summary>
public sealed record TutorScopeDto(
    Guid Id,
    TutorScopeLevel Level,
    Guid FacultyId,
    Guid? DepartmentId,
    Guid? DirectionId,
    Guid? GroupId,
    string Name,
    string Path,
    int Groups,
    int Students);

/// <summary><c>PUT .../scopes</c> body elementi: daraja + shu darajadagi tugun id'si.</summary>
public sealed record TutorScopeInput(TutorScopeLevel Level, Guid Id);

/// <summary>Tyutor fakultetlaridan birining daraxti (faqat faol tugunlar) — biriktirish oynasi uchun; <c>GET .../scope-tree</c>
/// har fakultet uchun bittadan massiv qaytaradi. Har tugunda <c>TutorId</c>/<c>TutorName</c> — AYNAN shu tugunda faol
/// ko'lami bor tyutor (so'ralayotganning o'zi ham), yo'q bo'lsa null.</summary>
public sealed record TutorScopeTree(
    Guid Id,
    string Name,
    string Code,
    Guid? TutorId,
    string? TutorName,
    IReadOnlyList<TutorScopeTreeDepartment> Departments);

public sealed record TutorScopeTreeDepartment(
    Guid Id,
    string Name,
    Guid? TutorId,
    string? TutorName,
    IReadOnlyList<TutorScopeTreeDirection> Directions);

public sealed record TutorScopeTreeDirection(
    Guid Id,
    string Name,
    Guid? TutorId,
    string? TutorName,
    IReadOnlyList<TutorScopeTreeGroup> Groups);

public sealed record TutorScopeTreeGroup(
    Guid Id,
    string Name,
    int Course,
    int Students,
    Guid? TutorId,
    string? TutorName);
