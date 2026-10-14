namespace Amaliyotchi.Domain.Attendance;

/// <summary>Admin ko'radigan "hozir qayerda" holati (docs/LIVE-JOYLASHUV.md, <c>presence.state</c>).</summary>
public enum PresenceState
{
    Inside = 1,
    Outside = 2,
    Inaccurate = 3,

    /// <summary>Sessiya faol, lekin oxirgi ping (yoki ping bo'lmasa — check-in) <c>3 × intervalSec</c> dan eski.</summary>
    SignalLost = 4,

    /// <summary>Check-out bo'lgan yoki kun avtomatik yopilgan.</summary>
    Stopped = 5,

    /// <summary>Sessiya faol, lekin talaba fonda kuzatuvga rozilik bermagan.</summary>
    NoConsent = 6
}

/// <summary>Hisob uchun bitta ping (vaqt bo'yicha tartiblangan ro'yxatda).</summary>
public readonly record struct PingSample(DateTimeOffset At, LocationPingStatus Status, double DistanceM, bool IsMocked);

/// <summary>Presence hisobi kirishi.</summary>
/// <param name="CheckInAt">Bugungi check-in vaqti.</param>
/// <param name="CheckOutAt">Check-out (yoki avtomatik yopilish) vaqti; yo'q — null.</param>
/// <param name="AutoCloseAt">Kun avtomatik yopiladigan moment (auto-close); shu vaqtdan keyin sessiya tugagan hisoblanadi.</param>
/// <param name="CheckInDistanceM">Check-in masofasi — hali ping bo'lmaganda ko'rsatiladi.</param>
/// <param name="Pings">Bugungi ping'lar, <see cref="PingSample.At"/> o'sish tartibida.</param>
public sealed record PresenceInput(
    DateTimeOffset CheckInAt,
    DateTimeOffset? CheckOutAt,
    DateTimeOffset AutoCloseAt,
    double? CheckInDistanceM,
    bool ConsentGiven,
    IReadOnlyList<PingSample> Pings,
    DateTimeOffset Now,
    int IntervalSec);

/// <param name="LastPingAt">Oxirgi ping vaqti; ping bo'lmasa null.</param>
/// <param name="DistanceM">Oxirgi ping masofasi (ping bo'lmasa — check-in masofasi).</param>
/// <param name="OutsideMinutes"><see cref="LocationTracking.OutsideMinutes"/>.</param>
/// <param name="Mocked">Bugun kamida bitta <c>isMocked</c> ping bo'lganmi.</param>
public sealed record PresenceSnapshot(
    PresenceState State,
    DateTimeOffset? LastPingAt,
    double? DistanceM,
    int OutsideMinutes,
    bool Mocked);

/// <summary>Fonda kuzatuvning sof qoidalari (I/O yo'q) — docs/LIVE-JOYLASHUV.md "Qoidalar" bo'limi.
/// Geozona qoidasi check-in bilan bir xil (<see cref="CheckInPolicy.IsAccuracyOk"/>, <see cref="CheckInPolicy.IsWithinRadius"/>).</summary>
public static class LocationTracking
{
    /// <summary>Bitta so'rovdagi maksimal ping soni.</summary>
    public const int MaxPingsPerBatch = 100;

    /// <summary>Ping'lar shuncha kundan keyin o'chiriladi.</summary>
    public const int RetentionDays = 60;

    /// <summary>Bitta oraliq <c>GapCapFactor × intervalSec</c> dan oshsa, <c>outsideMinutes</c> ga shu qiymat qo'shiladi.</summary>
    public const int GapCapFactor = 2;

    /// <summary>Oxirgi ping <c>SignalLostFactor × intervalSec</c> dan eski bo'lsa — <see cref="PresenceState.SignalLost"/>.</summary>
    public const int SignalLostFactor = 3;

    /// <summary><c>occurredAt</c> shundan eski bo'lsa ping tashlanadi.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    /// <summary><c>occurredAt</c> shundan ko'proq kelajakda bo'lsa ping tashlanadi.</summary>
    public static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(2);

    /// <summary>Ping holati: aniqlik chegaradan yomon → <see cref="LocationPingStatus.Inaccurate"/>; aks holda
    /// masofa ≤ radius → <see cref="LocationPingStatus.Inside"/>, bo'lmasa <see cref="LocationPingStatus.Outside"/>.</summary>
    public static LocationPingStatus Classify(double accuracyM, double distanceM, int radiusM, double minAccuracyM)
    {
        if (!CheckInPolicy.IsAccuracyOk(accuracyM, minAccuracyM))
            return LocationPingStatus.Inaccurate;
        return CheckInPolicy.IsWithinRadius(distanceM, radiusM) ? LocationPingStatus.Inside : LocationPingStatus.Outside;
    }

    /// <summary>Idempotentlik kaliti: UTC, millisekundgacha qisqartirilgan (Postgres mikrosekund saqlaydi — so'rovdagi
    /// 100 ns qismi takrorni "boshqa ping" qilib qo'ymasin).</summary>
    public static DateTimeOffset NormalizeInstant(DateTimeOffset at)
    {
        var utc = at.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMillisecond, TimeSpan.Zero);
    }

    /// <summary>Ping qabul oynasi: <c>[now − 24 soat, now + 2 daqiqa]</c>.</summary>
    public static bool IsWithinAcceptWindow(DateTimeOffset occurredAt, DateTimeOffset now)
        => occurredAt >= now - MaxAge && occurredAt <= now + FutureTolerance;

    /// <summary>Tashqarida o'tgan vaqt (daqiqa, pastga yaxlitlangan): ketma-ket ping'lar orasidagi vaqt oldingi ping
    /// <see cref="LocationPingStatus.Outside"/> bo'lsa qo'shiladi; bitta oraliq <c>2 × intervalSec</c> dan oshsa shu
    /// qiymatda kesiladi (signal uzilishi tashqarida bo'lish deb hisoblanmaydi). Oxirgi pingdan keyingi vaqt hisoblanmaydi.</summary>
    public static int OutsideMinutes(IReadOnlyList<PingSample> ordered, int intervalSec)
    {
        ArgumentNullException.ThrowIfNull(ordered);
        var cap = TimeSpan.FromSeconds((double)GapCapFactor * Math.Max(intervalSec, 1));
        var total = TimeSpan.Zero;
        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i - 1].Status != LocationPingStatus.Outside)
                continue;
            var gap = ordered[i].At - ordered[i - 1].At;
            if (gap <= TimeSpan.Zero)
                continue;
            total += gap > cap ? cap : gap;
        }

        return (int)Math.Floor(total.TotalMinutes);
    }

    /// <summary>Ketma-ket ping'lar orasidagi eng uzun oraliq (daqiqa, pastga yaxlitlangan); 2 tadan kam ping — 0.</summary>
    public static int LongestGapMinutes(IReadOnlyList<PingSample> ordered)
    {
        ArgumentNullException.ThrowIfNull(ordered);
        var longest = TimeSpan.Zero;
        for (var i = 1; i < ordered.Count; i++)
        {
            var gap = ordered[i].At - ordered[i - 1].At;
            if (gap > longest)
                longest = gap;
        }

        return (int)Math.Floor(longest.TotalMinutes);
    }

    /// <summary>Signal yo'qolganmi: oxirgi kuzatuv (ping, ping bo'lmasa — check-in) <c>3 × intervalSec</c> dan eski.</summary>
    public static bool IsSignalLost(DateTimeOffset lastSeenAt, DateTimeOffset now, int intervalSec)
        => now - lastSeenAt > TimeSpan.FromSeconds((double)SignalLostFactor * Math.Max(intervalSec, 1));

    /// <summary>Presence holati. Tartib: check-out/avto-yopilish → <c>stopped</c>; rozilik yo'q → <c>noConsent</c>;
    /// oxirgi kuzatuv eski → <c>signalLost</c>; aks holda oxirgi ping holati. Check-in'dan beri ping yo'q va hali
    /// <c>3 × intervalSec</c> o'tmagan bo'lsa — <c>inside</c> (qabul qilingan check-in radius ichida bo'lgan),
    /// <c>lastPingAt = null</c>, masofa — check-in'niki.</summary>
    public static PresenceSnapshot Evaluate(PresenceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var pings = input.Pings;
        PingSample? last = pings.Count > 0 ? pings[^1] : null;
        var outside = OutsideMinutes(pings, input.IntervalSec);
        var mocked = pings.Any(p => p.IsMocked);
        var lastAt = last?.At;
        var distance = last is { } l ? l.DistanceM : input.CheckInDistanceM;

        PresenceState state;
        if (input.CheckOutAt is not null || input.Now >= input.AutoCloseAt)
            state = PresenceState.Stopped;
        else if (!input.ConsentGiven)
            state = PresenceState.NoConsent;
        else if (IsSignalLost(lastAt ?? input.CheckInAt, input.Now, input.IntervalSec))
            state = PresenceState.SignalLost;
        else
            state = last?.Status switch
            {
                LocationPingStatus.Outside => PresenceState.Outside,
                LocationPingStatus.Inaccurate => PresenceState.Inaccurate,
                _ => PresenceState.Inside
            };

        return new PresenceSnapshot(state, lastAt, distance, outside, mocked);
    }
}
