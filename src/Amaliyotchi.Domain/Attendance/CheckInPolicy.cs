using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;

namespace Amaliyotchi.Domain.Attendance;

/// <summary>Bitta check-in urinishining sharoiti. Masofa bazadan (PostGIS) keladi, vaqt — Toshkent bo'yicha.</summary>
/// <param name="Date">Belgilanayotgan kun (Toshkent).</param>
/// <param name="LocalNow">Server vaqti Toshkent bo'yicha.</param>
/// <param name="ApplicationApproved">Talabaning arizasi tasdiqlanganmi.</param>
/// <param name="PeriodStarted">Davr boshlanganmi (Date ≥ StartDate).</param>
/// <param name="PeriodEnded">Davr tugaganmi (Date &gt; EndDate yoki Closed).</param>
/// <param name="WorkDays">Davrning ish kunlari.</param>
/// <param name="IsHoliday">Kun bayrammi.</param>
/// <param name="HasApprovedLeave">Shu kunga tasdiqlangan ruxsat bormi.</param>
/// <param name="AlreadyCheckedIn">Bugun check-in allaqachon bormi.</param>
/// <param name="AccuracyM">Qurilma GPS aniqligi (m).</param>
/// <param name="DistanceM">Korxonagacha masofa (m).</param>
/// <param name="RadiusM">Korxona geofence radiusi (m).</param>
/// <param name="QrValid">Skanerlangan QR korxonaga mosmi. QR yuborilmagan (va majburiy emas) bo'lsa — <c>true</c>.</param>
public sealed record CheckInContext(
    DateOnly Date,
    TimeOnly LocalNow,
    bool ApplicationApproved,
    bool PeriodStarted,
    bool PeriodEnded,
    WorkDays WorkDays,
    bool IsHoliday,
    bool HasApprovedLeave,
    bool AlreadyCheckedIn,
    double AccuracyM,
    double DistanceM,
    int RadiusM,
    bool QrValid = true)
{
    public bool IsWorkDay => WorkDays.Includes(Date) && !IsHoliday;
}

/// <summary>Bitta check-out urinishining sharoiti.</summary>
/// <param name="HasCheckedIn">Bugun check-in bormi.</param>
/// <param name="AlreadyCheckedOut">Check-out allaqachon bormi.</param>
/// <param name="AutoClosed">Kun avtomatik yopilganmi.</param>
/// <param name="QrValid">Skanerlangan QR korxonaga mosmi (yuborilmagan bo'lsa — <c>true</c>).</param>
public sealed record CheckOutContext(
    TimeOnly LocalNow,
    bool HasCheckedIn,
    bool AlreadyCheckedOut,
    bool AutoClosed,
    double AccuracyM,
    double DistanceM,
    int RadiusM,
    bool QrValid = true);

/// <summary>Siyosat qarori: qabul qilindi (kech/kech emas) yoki rad (sabab bilan).</summary>
/// <param name="FaceMatchScore">Yuzni tasdiqlash ishlagan bo'lsa — selfi va etalon o'xshashligi (0–100), aks holda null.</param>
public sealed record CheckInVerdict(bool Accepted, CheckInRejectReason Reason, bool IsLate, int? FaceMatchScore = null)
{
    public static CheckInVerdict Accept(bool isLate = false) => new(true, CheckInRejectReason.None, isLate);

    public static CheckInVerdict Reject(CheckInRejectReason reason) => new(false, reason, false);

    /// <summary>Rad etilgan urinishni API xatosiga aylantiradi (yuz balli bilan).</summary>
    public DomainException ToException() => Reason.ToException(FaceMatchScore);

    /// <summary>Qabul qilingan check-in uchun davomat holati.</summary>
    public AttendanceStatus Status => IsLate ? AttendanceStatus.Late : AttendanceStatus.Present;
}

/// <summary>Geofence check-in dvigatelining sof qismi: hech qanday I/O yo'q, hammasi kontekstdan.
/// Tekshiruv tartibi muhim — foydalanuvchi eng "asosiy" sababni ko'rishi kerak
/// (ariza → davr → ish kuni → ruxsat → takror → oyna → QR → GPS → radius). QR oynadan keyin: yopiq oynada
/// "QR noto'g'ri" emas, oyna sababi ko'rinadi; GPS'dan oldin: QR — joyda turganlikning kuchliroq isboti.</summary>
public static class CheckInPolicy
{
    public static CheckInVerdict Evaluate(CheckInContext ctx, CheckInRules rules)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(rules);

        if (!ctx.ApplicationApproved)
            return CheckInVerdict.Reject(CheckInRejectReason.NotApproved);
        if (!ctx.PeriodStarted)
            return CheckInVerdict.Reject(CheckInRejectReason.PeriodNotStarted);
        if (ctx.PeriodEnded)
            return CheckInVerdict.Reject(CheckInRejectReason.PeriodEnded);
        if (!ctx.IsWorkDay)
            return CheckInVerdict.Reject(CheckInRejectReason.NotWorkDay);
        if (ctx.HasApprovedLeave)
            return CheckInVerdict.Reject(CheckInRejectReason.OnLeave);
        if (ctx.AlreadyCheckedIn)
            return CheckInVerdict.Reject(CheckInRejectReason.AlreadyCheckedIn);
        if (ctx.LocalNow < rules.DailyStart)
            return CheckInVerdict.Reject(CheckInRejectReason.WindowNotOpen);
        if (ctx.LocalNow >= rules.WindowEnd)
            return CheckInVerdict.Reject(CheckInRejectReason.WindowClosed);
        if (!ctx.QrValid)
            return CheckInVerdict.Reject(CheckInRejectReason.QrInvalid);
        if (!IsAccuracyOk(ctx.AccuracyM, rules.MinAccuracyM))
            return CheckInVerdict.Reject(CheckInRejectReason.PoorAccuracy);
        if (!IsWithinRadius(ctx.DistanceM, ctx.RadiusM))
            return CheckInVerdict.Reject(CheckInRejectReason.OutOfRadius);

        return CheckInVerdict.Accept(isLate: ctx.LocalNow >= rules.LateAfter);
    }

    public static CheckInVerdict EvaluateCheckOut(CheckOutContext ctx, CheckInRules rules)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(rules);

        if (!ctx.HasCheckedIn)
            return CheckInVerdict.Reject(CheckInRejectReason.NoCheckIn);
        if (ctx.AlreadyCheckedOut || ctx.AutoClosed)
            return CheckInVerdict.Reject(CheckInRejectReason.AlreadyCheckedOut);
        if (ctx.LocalNow < rules.CheckOutFrom)
            return CheckInVerdict.Reject(CheckInRejectReason.WindowNotOpen);
        if (ctx.LocalNow >= rules.AutoCloseAt)
            return CheckInVerdict.Reject(CheckInRejectReason.WindowClosed);
        if (!ctx.QrValid)
            return CheckInVerdict.Reject(CheckInRejectReason.QrInvalid);
        if (!IsAccuracyOk(ctx.AccuracyM, rules.MinAccuracyM))
            return CheckInVerdict.Reject(CheckInRejectReason.PoorAccuracy);
        if (!IsWithinRadius(ctx.DistanceM, ctx.RadiusM))
            return CheckInVerdict.Reject(CheckInRejectReason.OutOfRadius);

        return CheckInVerdict.Accept();
    }

    /// <summary>Aniqlik chegaraga teng bo'lsa — qabul (100 m ≤ 100 m).</summary>
    public static bool IsAccuracyOk(double accuracyM, double minAccuracyM)
        => !double.IsNaN(accuracyM) && accuracyM >= 0 && accuracyM <= minAccuracyM;

    /// <summary>Masofa radiusga teng bo'lsa — ichida (chegara talaba foydasiga).</summary>
    public static bool IsWithinRadius(double distanceM, int radiusM)
        => !double.IsNaN(distanceM) && distanceM >= 0 && distanceM <= radiusM;
}
