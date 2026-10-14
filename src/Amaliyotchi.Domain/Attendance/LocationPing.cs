using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.ValueObjects;

namespace Amaliyotchi.Domain.Attendance;

/// <summary>Ping holati (server hisoblaydi — <see cref="LocationTracking.Classify"/>).</summary>
public enum LocationPingStatus
{
    /// <summary>Masofa ≤ radius.</summary>
    Inside = 1,

    /// <summary>Masofa &gt; radius.</summary>
    Outside = 2,

    /// <summary>GPS aniqligi chegaradan yomon — na ichkari, na tashqari hisoblanadi.</summary>
    Inaccurate = 3
}

/// <summary>Ping qayerdan olingan: ilova ekrani ochiq (<see cref="Foreground"/>) yoki fonda (<see cref="Background"/>).</summary>
public enum LocationPingSource
{
    Foreground = 1,
    Background = 2
}

/// <summary>Ish vaqtida fonda kuzatuv nuqtasi (docs/LIVE-JOYLASHUV.md): faqat check-in'dan check-out'gacha (yoki
/// avtomatik yopilish vaqtigacha) bo'lgan davomat sessiyasiga tegishli. O'zgarmas; yuqori hajmli texnik yozuv —
/// audit'dan istisno (<see cref="IAuditExempt"/>), 60 kundan keyin fon job o'chiradi
/// (<see cref="LocationTracking.RetentionDays"/>). (talaba, <see cref="OccurredAt"/>) — noyob (idempotentlik).</summary>
public sealed class LocationPing : BaseEntity, IAuditExempt
{
    private LocationPing() { }

    public Guid StudentUserId { get; private set; }

    /// <summary>Ping tegishli davomat yozuvi (sessiya).</summary>
    public Guid AttendanceId { get; private set; }

    /// <summary>Toshkent kuni (davomat yozuvi kuni).</summary>
    public DateOnly Date { get; private set; }

    /// <summary>Qurilma vaqti (UTC, millisekundgacha qisqartirilgan — idempotentlik kaliti).</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    public GeoPoint Location { get; private set; }
    public double AccuracyM { get; private set; }
    public double DistanceM { get; private set; }

    /// <summary>Ping paytidagi korxona radiusi (keyin o'zgarsa ham tarix saqlanadi).</summary>
    public int RadiusM { get; private set; }

    public LocationPingStatus Status { get; private set; }

    /// <summary>Klient "soxta joylashuv" deb belgilagan — rad etilmaydi, adminga belgi.</summary>
    public bool IsMocked { get; private set; }

    public LocationPingSource Source { get; private set; }

    /// <summary>Server qabul qilgan vaqt.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    public static LocationPing Record(
        Guid studentUserId,
        Guid attendanceId,
        DateOnly date,
        DateTimeOffset occurredAt,
        GeoPoint location,
        double accuracyM,
        double distanceM,
        int radiusM,
        double minAccuracyM,
        bool isMocked,
        LocationPingSource source,
        DateTimeOffset createdAt)
    {
        if (studentUserId == Guid.Empty)
            throw new DomainException("Talaba ko'rsatilmagan.");
        if (attendanceId == Guid.Empty)
            throw new DomainException("Davomat yozuvi ko'rsatilmagan.");
        if (double.IsNaN(accuracyM) || double.IsInfinity(accuracyM) || accuracyM < 0)
            throw new DomainException("GPS aniqligi manfiy bo'lmagan son bo'lishi kerak.");

        return new LocationPing
        {
            StudentUserId = studentUserId,
            AttendanceId = attendanceId,
            Date = date,
            OccurredAt = LocationTracking.NormalizeInstant(occurredAt),
            Location = location,
            AccuracyM = accuracyM,
            DistanceM = distanceM,
            RadiusM = radiusM,
            Status = LocationTracking.Classify(accuracyM, distanceM, radiusM, minAccuracyM),
            IsMocked = isMocked,
            Source = source,
            CreatedAt = createdAt
        };
    }
}
