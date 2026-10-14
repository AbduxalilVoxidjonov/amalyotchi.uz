using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;

namespace Amaliyotchi.Application.Features.Student.Tracking;

/// <summary><see cref="TrackingDto"/> ni quradi — <c>GET today</c>, check-in/check-out javobi va ping javobi bitta qoidadan.</summary>
internal static class TrackingState
{
    /// <summary>Kunning avtomatik yopilish momenti (UTC) — mavjud auto-close qoidasi (<see cref="CheckInRules.AutoCloseAt"/>,
    /// ish tugashi + <c>autoCheckout</c> daqiqa; undan keyin check-out qabul qilinmaydi).</summary>
    public static DateTimeOffset AutoCloseMoment(DateOnly date, CheckInRules rules)
        => PracticeTime.At(date, rules.AutoCloseAt).ToUniversalTime();

    /// <summary>Kuzatuv sessiyasi faolmi: check-in bor, check-out yo'q, kun avtomatik yopilmagan va yopilish vaqti o'tmagan.</summary>
    public static bool IsSessionActive(DailyAttendance? row, DateOnly date, CheckInRules rules, DateTimeOffset now)
        => row is { HasCheckedIn: true, HasCheckedOut: false, AutoClosed: false } && now < AutoCloseMoment(date, rules);

    public static TrackingDto Build(
        StudentSettings settings, bool consentGiven, DailyAttendance? todayRow, DateOnly today, CheckInRules rules, DateTimeOffset now)
    {
        var active = IsSessionActive(todayRow, today, rules, now);
        return new TrackingDto(
            settings.TrackingEnabled && active,
            consentGiven,
            settings.TrackingIntervalSec,
            settings.TrackingDistanceFilterM,
            active ? AutoCloseMoment(today, rules) : null);
    }

    /// <summary>Davr/sessiya yo'q holat: <c>enabled=false</c>, sozlama qiymatlari bilan.</summary>
    public static TrackingDto Inactive(StudentSettings settings, bool consentGiven)
        => new(false, consentGiven, settings.TrackingIntervalSec, settings.TrackingDistanceFilterM, null);
}
