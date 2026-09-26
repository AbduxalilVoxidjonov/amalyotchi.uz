using Amaliyotchi.Application.Common.Practice;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Practice;

namespace Amaliyotchi.Application.Features.Student;

// Talaba (TWA) kontrakti v2 (PLAN §3.1): enum'lar camelCase string, qiymatlar xom, soatlar "HH:mm" (Toshkent).

/// <summary><c>GET /api/student/today</c>. Ariza yo'q bo'lsa <see cref="Place"/> null, <c>checkin.status = pending</c>
/// va <c>checkin.note</c> da sabab. <see cref="Period"/> — ko'rsatilayotgan davr (davom etayotgan → eng yaqin kelgusi →
/// oxirgi tugagan); davr yo'q bo'lsa null. Belgilanish faqat <c>period.status = active</c> va bugun davr ichida bo'lsa mumkin.</summary>
public sealed record TodayDto(
    DateOnly Date,
    TodayWindowDto Window,
    TodayCheckInDto Checkin,
    TodayPlaceDto? Place,
    TodayDiaryDto Diary,
    StudentPeriodOption? Period);

/// <param name="Start">Check-in ochiladigan vaqt (09:00).</param>
/// <param name="End">Shu vaqtdan boshlab "kech keldi" (09:15).</param>
/// <param name="ClosesAt">Shu vaqtdan check-in qabul qilinmaydi (10:30).</param>
/// <param name="CheckoutAt">Check-out ochiladigan vaqt (17:00).</param>
/// <param name="IsOpen">Hozirgi bosqich uchun amal (check-in yoki check-out) server vaqti bo'yicha mumkinmi.</param>
public sealed record TodayWindowDto(string Start, string End, string ClosesAt, string CheckoutAt, bool IsOpen);

/// <param name="Note">Amal hozir mumkin bo'lmasa — o'zbekcha sabab (ariza yo'q, ish kuni emas, oyna yopiq …).</param>
/// <param name="PhotoRequired">Sozlama <c>checkinPhotoRequired</c>: <c>true</c> bo'lsa check-in/check-out selfisiz qabul qilinmaydi.</param>
/// <param name="QrRequired">Sozlama <c>checkinQrRequired</c>: <c>true</c> bo'lsa check-in/check-out uchun korxona QR kodi skanerlanishi shart.</param>
public sealed record TodayCheckInDto(
    AttendanceStatus Status,
    DateTimeOffset? CheckInAt,
    DateTimeOffset? CheckOutAt,
    double? DistanceM,
    int? RadiusM,
    double? GpsAccuracyM,
    bool Suspicious,
    bool AutoClosed,
    string? Note,
    bool PhotoRequired,
    bool QrRequired);

public sealed record TodayPlaceDto(
    string Company,
    string Address,
    int RadiusM,
    double AttendancePct,
    int DaysPresent,
    int DaysTotal,
    int Reports,
    double AvgScore);

/// <param name="PdfRequired">Sozlama <c>diaryPdfRequired</c>: <c>true</c> bo'lsa hisobotga kamida bitta PDF biriktirilishi shart.</param>
public sealed record TodayDiaryDto(bool SubmittedToday, int MinChars, int MaxFiles, bool PdfRequired);

/// <summary><c>GET /api/student/place</c>. Ariza yo'q → 404.</summary>
/// <param name="PeriodId">Javobdagi arizaning davri.</param>
/// <param name="PeriodName">Shu davr nomi.</param>
/// <param name="IsPast"><c>true</c> — davr yopilgan (<c>Closed</c>) yoki tugagan (<c>EndDate &lt; bugun</c>, Toshkent):
/// talaba hozir bu korxonaga biriktirilmagan, ilova bo'limni "O'tgan amaliyot davri" deb ko'rsatadi.</param>
public sealed record PracticePlaceDto(
    ApplicationStatus Status,
    string? Comment,
    string Company,
    string Tin,
    string Activity,
    string Address,
    string SupervisorName,
    string SupervisorPhone,
    string? MentorName,
    string? MentorPhone,
    int RadiusM,
    double Lat,
    double Lng,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    PracticeContractDto? Contract,
    Guid PeriodId,
    string PeriodName,
    bool IsPast);

public sealed record PracticeContractDto(
    Guid FileId,
    string FileName,
    int? Pages,
    long SizeBytes,
    DateTimeOffset UploadedAt,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    string? TemplateUrl);

/// <summary><c>GET/POST /api/student/diary</c>. <see cref="PeriodId"/>/<see cref="PeriodName"/> — yozuv tegishli davr
/// (tarix bir nechta davrni qamraydi; o'chirilgan davr nomi null).</summary>
public sealed record DiaryEntryDto(
    Guid Id,
    DateOnly Date,
    DateTimeOffset SubmittedAt,
    DiaryStatus Status,
    string Text,
    string? Learned,
    IReadOnlyList<DiaryFileDto> Files,
    int? Score,
    string? Comment,
    Guid PeriodId,
    string? PeriodName);

public sealed record DiaryFileDto(Guid Id, string Name, string Url);

/// <summary><c>GET /api/student/calendar?month=YYYY-MM</c>.</summary>
public sealed record CalendarMonthDto(
    string Month,
    string StudentName,
    string GroupName,
    IReadOnlyList<CalendarDayDto> Days);

public sealed record CalendarDayDto(DateOnly Date, CalendarDayStatus Status);

/// <summary><c>GET /api/student/portfolio?periodId=</c> — tanlangan davr (berilmasa sukut bo'yicha: davom etayotgan →
/// oxirgi tugagan → kelgusi) bo'yicha. <see cref="Periods"/> — talabaning barcha davrlari (tarix), <c>startDate</c> kamayish tartibida.</summary>
public sealed record PortfolioDto(
    string Student,
    string Group,
    string PracticeTitle,
    string? Company,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    PortfolioStatsDto Stats,
    IReadOnlyList<PortfolioScoreDto> Score,
    double Total,
    int? Grade,
    bool Finalized,
    PortfolioConclusionDto? Conclusion,
    string? PdfUrl,
    Guid PeriodId,
    IReadOnlyList<StudentPeriodOption> Periods);

public sealed record PortfolioStatsDto(
    double AttendancePct,
    int DaysPresent,
    int DaysTotal,
    int Late,
    int Excused,
    int Reports,
    double AvgScore);

/// <param name="Key">attendance · reports · tutor · reference.</param>
public sealed record PortfolioScoreDto(string Key, int WeightPct, double Points);

public sealed record PortfolioConclusionDto(string Text, string Author, DateTimeOffset Date);
