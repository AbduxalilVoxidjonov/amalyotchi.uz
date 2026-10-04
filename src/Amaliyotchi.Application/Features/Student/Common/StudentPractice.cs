using System.Globalization;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Practice;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.Domain.Students;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Student.Common;

/// <summary>Tizim sozlamalaridan talaba oqimiga kerakli qismi (yo'q kalit → domain default).
/// <see cref="FaceVerificationEnabled"/> yoqilgan bo'lsa check-in selfisi <see cref="CheckInPhotoRequired"/> dan qat'i nazar majburiy.</summary>
internal sealed record StudentSettings(
    double MinGpsAccuracyM, int MinReportLength, bool CheckInPhotoRequired, bool DiaryPdfRequired, bool CheckInQrRequired,
    bool FaceVerificationEnabled = false, int FaceMatchThreshold = 36);

/// <summary>Bitta talabaning amaliyot holati: profil (foydalanuvchi + guruh), sirt maqsadiga ko'ra tanlangan davr
/// (<see cref="PeriodSelection"/>), shu davr bo'yicha ariza (korxona bilan), talabaning barcha davrlari, bayramlar,
/// sozlamalar. Barcha talaba handler'lari shu yerdan boshlanadi — ko'lam <c>ICurrentUser.UserId</c> bilan qat'iy cheklangan.</summary>
internal sealed record StudentPractice(
    StudentProfile Student,
    PracticePeriod? Period,
    PracticeApplication? Application,
    IReadOnlyList<Holiday> Holidays,
    StudentSettings Settings,
    StudentPeriodSet Periods,
    DateOnly Today)
{
    /// <summary>Tanlangan davr bugun davom etayaptimi (yopilmagan va bugunni o'z ichiga oladi) — check-in shu holatda mumkin.</summary>
    public bool IsOngoing => Period is not null && PeriodSelection.IsOngoing(Period, Today);

    /// <summary>Davom etayotgan davr yo'q bo'lsa talabaga ko'rsatiladigan sabab: kelgusi davr bo'lsa
    /// "Amaliyot davri hali boshlanmagan: &lt;nom&gt;, &lt;dd.MM.yyyy&gt; dan boshlanadi.", tugagan bo'lsa
    /// "Amaliyot davri tugagan: &lt;nom&gt;.", umuman bo'lmasa — "Faol amaliyot davri yo'q.".</summary>
    public string NoOngoingNote()
    {
        var groupPeriods = Periods.GroupPeriods;
        if (PeriodSelection.Select(groupPeriods, Today, PeriodPurpose.Enrollment) is { } upcoming)
            return string.Create(CultureInfo.InvariantCulture,
                $"{CheckInRejectReason.PeriodNotStarted.Message().TrimEnd('.')}: {upcoming.Name}, {upcoming.StartDate:dd.MM.yyyy} dan boshlanadi.");
        if (PeriodSelection.LastEnded(groupPeriods, Today, PeriodSpan.Of) is { } ended)
            return $"{CheckInRejectReason.PeriodEnded.Message().TrimEnd('.')}: {ended.Name}.";
        return NoPeriodMessage;
    }

    public const string NoPeriodMessage = "Faol amaliyot davri yo'q.";

    /// <summary>Korxona — faqat ariza bor va korxona o'chirilmagan bo'lsa.</summary>
    public Company? Company => Application?.Company;

    /// <summary>Check-in uchun shart: tasdiqlangan ariza + korxona.</summary>
    public bool IsApproved => Application is { Status: ApplicationStatus.Approved } && Company is not null;

    /// <summary>Bugungi qoidalar: davr qoidalari (davr bo'lmasa — standart), GPS aniqligi sozlamadan; talaba o'z ish
    /// vaqtini belgilagan va u bugun amalda bo'lsa (<see cref="StudentProfile.HoursOn"/>) — kelish/ketish shu soatlardan,
    /// daqiqa qoidalari davrnikidan. Check-in, check-out, "bugun" va kalendar shu qoidadan foydalanadi.</summary>
    public CheckInRules Rules => Student.HoursOn(Today) is { } own ? PeriodRules.WithHours(own.Start, own.End) : PeriodRules;

    /// <summary>Faqat davr (yoki standart) qoidalari — talabaning o'z soatlarisiz.</summary>
    public CheckInRules PeriodRules => Period?.Rules(Settings.MinGpsAccuracyM)
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
    /// <summary>Talaba profili + davr + ariza + bayramlar + sozlamalar. Profil yo'q → 404.
    /// Davr: <paramref name="periodId"/> berilsa — talabaning davrlaridan biri (aks holda 404), berilmasa —
    /// <paramref name="purpose"/> qoidasi bilan (<see cref="PeriodSelection"/>).</summary>
    public static async Task<StudentPractice> LoadStudentPracticeAsync(
        this IApplicationDbContext db,
        Guid studentUserId,
        DateOnly today,
        PeriodPurpose purpose,
        CancellationToken cancellationToken,
        Guid? periodId = null)
    {
        var student = await db.StudentProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.Group)
            .FirstOrDefaultAsync(p => p.UserId == studentUserId, cancellationToken)
            ?? throw new NotFoundException("Talaba profili topilmadi.");

        var periods = await db.LoadStudentPeriodsAsync(studentUserId, student.StudentGroupId, cancellationToken);
        var period = periods.Resolve(periodId, today, purpose);

        PracticeApplication? application = null;
        if (period is not null)
        {
            var selectedId = period.Id;
            var applications = await db.PracticeApplications
                .AsNoTracking()
                .Include(a => a.Company)
                .Where(a => a.StudentUserId == studentUserId && a.PeriodId == selectedId)
                .OrderByDescending(a => a.SubmittedAt)
                .ToListAsync(cancellationToken);

            // Tasdiqlangan ariza ustun; bo'lmasa eng so'nggi (rad etilgan/qaytarilgan holatini ko'rsatish uchun).
            // O'tkazilgan (Transferred) ariza — faqat tarix: joriy ariza sifatida tanlanmaydi.
            application = applications.FirstOrDefault(a => a.Status == ApplicationStatus.Approved)
                ?? applications.FirstOrDefault(a => a.Status != ApplicationStatus.Transferred);
        }

        var holidays = await db.Holidays.AsNoTracking().ToListAsync(cancellationToken);
        var settings = await db.LoadStudentSettingsAsync(cancellationToken);

        return new StudentPractice(student, period, application, holidays, settings, periods, today);
    }

    public static async Task<StudentSettings> LoadStudentSettingsAsync(
        this IApplicationDbContext db, CancellationToken cancellationToken)
    {
        string[] keys =
        [
            SettingKeys.MinGpsAccuracy, SettingKeys.MinReportLength,
            SettingKeys.CheckInPhotoRequired, SettingKeys.DiaryPdfRequired, SettingKeys.CheckInQrRequired,
            SettingKeys.FaceVerificationEnabled, SettingKeys.FaceMatchThreshold
        ];
        var values = await db.AppSettings
            .AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, StringComparer.Ordinal, cancellationToken);

        return new StudentSettings(
            Int(values, SettingKeys.MinGpsAccuracy),
            Int(values, SettingKeys.MinReportLength),
            Bool(values, SettingKeys.CheckInPhotoRequired),
            Bool(values, SettingKeys.DiaryPdfRequired),
            Bool(values, SettingKeys.CheckInQrRequired),
            Bool(values, SettingKeys.FaceVerificationEnabled),
            Int(values, SettingKeys.FaceMatchThreshold));
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
