using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.ValueObjects;

namespace Amaliyotchi.Application.Features.Student.CheckIn;

/// <summary>Soxta lokatsiya belgilari (FUNKSIONAL-QOLLANMA §8.2): avtomatik jazolamaydi — faqat
/// <see cref="DailyAttendance.MarkSuspicious"/> uchun sabab qaytaradi. Ikkita arzon tekshiruv:
/// tezlik (oldingi urinishdan hozirgi nuqtaga fizik jihatdan yetib bo'lmaydi) va
/// kundan-kunga aynan bir xil koordinata (real GPS bunday bermaydi).</summary>
internal static class SuspiciousDetector
{
    /// <summary>Shundan tez "harakat" — shubhali (km/soat).</summary>
    public const double MaxSpeedKmh = 150;

    /// <summary>Oldingi urinish shundan eski bo'lsa tezlik tekshirilmaydi (soat).</summary>
    public const double SpeedWindowHours = 6;

    /// <summary>Shundan qisqa "sakrash" GPS tebranishi bo'lishi mumkin — tezlik tekshirilmaydi (km).</summary>
    public const double MinJumpKm = 1;

    public static string? Inspect(
        GeoPoint location, DateTimeOffset receivedAt, DateOnly today, IReadOnlyList<AttendanceEvent> recentEvents)
    {
        var previous = recentEvents
            .Where(e => e.ReceivedAt < receivedAt)
            .OrderByDescending(e => e.ReceivedAt)
            .FirstOrDefault();

        if (previous is not null)
        {
            var hours = (receivedAt - previous.ReceivedAt).TotalHours;
            if (hours is > 0 and <= SpeedWindowHours)
            {
                var km = previous.Location.DistanceMetersTo(location) / 1000d;
                var speed = km / hours;
                if (km >= MinJumpKm && speed > MaxSpeedKmh)
                    return FormattableString.Invariant($"Tezlik tahlili: {hours * 60:F0} daqiqada {km:F1} km ({speed:F0} km/soat).");
            }
        }

        var sameSpot = recentEvents
            .Where(e => e.Date < today && e.Accepted && e.Kind == AttendanceEventKind.CheckIn)
            .Any(e => e.Location == location);
        if (sameSpot)
            return "Kundan-kunga aynan bir xil koordinata.";

        return null;
    }
}
