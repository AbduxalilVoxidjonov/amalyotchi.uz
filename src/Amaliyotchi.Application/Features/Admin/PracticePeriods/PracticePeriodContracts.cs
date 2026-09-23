using Amaliyotchi.Domain.Practice;

namespace Amaliyotchi.Application.Features.Admin.PracticePeriods;

/// <summary>Ro'yxat qatori. <paramref name="Status"/> — hisoblangan holat (<see cref="PracticePeriod.EffectiveStatus"/>):
/// <c>planned</c> (boshlanish sanasi bugundan keyin, Toshkent), <c>active</c>, <c>closed</c> (faqat <c>/close</c> orqali).
/// <paramref name="StudentsCount"/> — biriktirilgan guruhlardagi faol (<c>StudentStatus.Active</c>) talabalar.</summary>
public sealed record PracticePeriodListItem(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    PracticePeriodStatus Status,
    int GroupsCount,
    int StudentsCount,
    DateTimeOffset CreatedAt);

/// <summary>Davrga biriktirilgan guruh (<paramref name="Id"/> — <c>StudentGroup.Id</c>, <paramref name="Code"/> — "412-22").</summary>
public sealed record PracticePeriodGroupDto(
    Guid Id,
    string Code,
    int Course,
    int StudentsCount,
    Guid FacultyId,
    string FacultyName,
    Guid DepartmentId,
    string DepartmentName,
    Guid DirectionId,
    string DirectionName);

/// <summary>Davr tafsiloti — ro'yxat qatori maydonlari + vaqt qoidalari va guruhlar.
/// <paramref name="DailyStart"/>/<paramref name="DailyEnd"/> — "HH:mm"; <paramref name="WorkDays"/> — "1,2,3,4,5,6".</summary>
public sealed record PracticePeriodDetail(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    PracticePeriodStatus Status,
    int GroupsCount,
    int StudentsCount,
    DateTimeOffset CreatedAt,
    string DailyStart,
    string DailyEnd,
    string WorkDays,
    int RequiredDays,
    bool DailyReportRequired,
    IReadOnlyList<PracticePeriodGroupDto> Groups);
