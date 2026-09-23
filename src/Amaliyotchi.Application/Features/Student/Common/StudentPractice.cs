using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.Domain.Students;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Student.Common;

/// <summary>Tizim sozlamalaridan talaba oqimiga kerakli qismi (yo'q kalit → domain default).</summary>
internal sealed record StudentSettings(
    double MinGpsAccuracyM, int MinReportLength, bool CheckInPhotoRequired, bool DiaryPdfRequired);

/// <summary>Bitta talabaning amaliyot holati: profil (foydalanuvchi + guruh), faol davr, davr bo'yicha ariza
/// (korxona bilan), bayramlar, sozlamalar. Barcha talaba handler'lari shu yerdan boshlanadi —
/// ko'lam <c>ICurrentUser.UserId</c> bilan qat'iy cheklangan.</summary>
internal sealed record StudentPractice(
    StudentProfile Student,
    PracticePeriod? Period,
    PracticeApplication? Application,
    IReadOnlyList<Holiday> Holidays,
    StudentSettings Settings)
{
    /// <summary>Korxona — faqat ariza bor va korxona o'chirilmagan bo'lsa.</summary>
    public Company? Company => Application?.Company;

    /// <summary>Check-in uchun shart: tasdiqlangan ariza + korxona.</summary>
    public bool IsApproved => Application is { Status: ApplicationStatus.Approved } && Company is not null;

    /// <summary>Davr qoidalari (davr bo'lmasa — standart), GPS aniqligi sozlamadan.</summary>
    public CheckInRules Rules => Period?.Rules(Settings.MinGpsAccuracyM)
        ?? new CheckInRules(
            CheckInRules.Default.DailyStart, CheckInRules.Default.DailyEnd,
            CheckInRules.Default.LateToleranceMinutes, CheckInRules.Default.CheckInWindowMinutes,
            CheckInRules.Default.CheckoutGraceMinutes, Settings.MinGpsAccuracyM);

    public bool IsHoliday(DateOnly date) => Holidays.Any(h => h.AppliesTo(date));

    /// <summary>Davr ichida, ish kunlariga kiradi va bayram emas.</summary>
    public bool IsWorkDay(DateOnly date) => Period is not null && Period.IsWorkDay(date, IsHoliday(date));

    /// <summary>Davrning [Start, min(End, today)] oralig'idagi ish kunlari (o'sish tartibida).</summary>
    public IEnumerable<DateOnly> WorkDaysUntil(DateOnly today)
    {
        if (Period is null)
            yield break;

        var last = Period.EndDate < today ? Period.EndDate : today;
        for (var date = Period.StartDate; date <= last; date = date.AddDays(1))
        {
            if (IsWorkDay(date))
                yield return date;
        }
    }
}

internal static class StudentPracticeLoader
{
    /// <summary>Talaba profili + davr + ariza + bayramlar + sozlamalar. Profil yo'q → 404.</summary>
    public static async Task<StudentPractice> LoadStudentPracticeAsync(
        this IApplicationDbContext db, Guid studentUserId, DateOnly today, CancellationToken cancellationToken)
    {
        var student = await db.StudentProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.Group)
            .FirstOrDefaultAsync(p => p.UserId == studentUserId, cancellationToken)
            ?? throw new NotFoundException("Talaba profili topilmadi.");

        var groupId = student.StudentGroupId;
        var periods = await db.PracticePeriods
            .AsNoTracking()
            .Where(p => p.Status == PracticePeriodStatus.Active && p.Groups.Any(g => g.StudentGroupId == groupId))
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(cancellationToken);

        // Bugunni o'z ichiga olgan faol davr; bo'lmasa — eng so'nggisi (davr tugagan/boshlanmagan holatlar uchun).
        var period = periods.FirstOrDefault(p => p.Contains(today)) ?? periods.FirstOrDefault();

        PracticeApplication? application = null;
        if (period is not null)
        {
            var periodId = period.Id;
            var applications = await db.PracticeApplications
                .AsNoTracking()
                .Include(a => a.Company)
                .Where(a => a.StudentUserId == studentUserId && a.PeriodId == periodId)
                .OrderByDescending(a => a.SubmittedAt)
                .ToListAsync(cancellationToken);

            // Tasdiqlangan ariza ustun; bo'lmasa eng so'nggi (rad etilgan/qaytarilgan holatini ko'rsatish uchun).
            application = applications.FirstOrDefault(a => a.Status == ApplicationStatus.Approved)
                ?? applications.FirstOrDefault();
        }

        var holidays = await db.Holidays.AsNoTracking().ToListAsync(cancellationToken);
        var settings = await db.LoadStudentSettingsAsync(cancellationToken);

        return new StudentPractice(student, period, application, holidays, settings);
    }

    public static async Task<StudentSettings> LoadStudentSettingsAsync(
        this IApplicationDbContext db, CancellationToken cancellationToken)
    {
        string[] keys =
        [
            SettingKeys.MinGpsAccuracy, SettingKeys.MinReportLength,
            SettingKeys.CheckInPhotoRequired, SettingKeys.DiaryPdfRequired
        ];
        var values = await db.AppSettings
            .AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, StringComparer.Ordinal, cancellationToken);

        return new StudentSettings(
            Int(values, SettingKeys.MinGpsAccuracy),
            Int(values, SettingKeys.MinReportLength),
            Bool(values, SettingKeys.CheckInPhotoRequired),
            Bool(values, SettingKeys.DiaryPdfRequired));
    }

    /// <summary>Mantiqiy sozlama: yo'q yoki buzuq qiymat → ta'rifdagi standart.</summary>
    private static bool Bool(IReadOnlyDictionary<string, string> values, string key)
    {
        var definition = SettingKeys.Get(key);
        var raw = values.GetValueOrDefault(key) ?? definition.DefaultValue;
        return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static int Int(IReadOnlyDictionary<string, string> values, string key)
    {
        var definition = SettingKeys.Get(key);
        var raw = values.GetValueOrDefault(key) ?? definition.DefaultValue;
        return int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n)
            ? n
            : int.Parse(definition.DefaultValue, System.Globalization.CultureInfo.InvariantCulture);
    }
}
