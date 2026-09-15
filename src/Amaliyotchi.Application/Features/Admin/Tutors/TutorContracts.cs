namespace Amaliyotchi.Application.Features.Admin.Tutors;

/// <summary>Tyutor kartasi — yaratish/tahrirlash/guruh biriktirish amallarining yagona javob shakli.
/// <paramref name="Groups"/> — faqat faol (o'chirilmagan) biriktiruvlar.</summary>
public sealed record TutorDetail(
    Guid Id,
    string FullName,
    string HemisId,
    string? Phone,
    Guid FacultyId,
    string FacultyCode,
    string FacultyName,
    bool IsActive,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt,
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

/// <summary>Tyutor fakultetidagi faol guruh — biriktirish oynasi uchun. <paramref name="TutorId"/>/<paramref name="TutorName"/> —
/// hozir faol biriktirilgan tyutor (shu tyutorning o'zi bo'lsa ham to'ldiriladi), bo'lmasa null.</summary>
public sealed record AvailableGroupRow(
    Guid Id,
    string Name,
    int Course,
    string DirectionName,
    string DepartmentName,
    int Students,
    Guid? TutorId,
    string? TutorName);
