using Amaliyotchi.Domain.Attendance;

namespace Amaliyotchi.Application.Features.Supervision.Tracking;

// Live joylashuv — kontrakt docs/LIVE-JOYLASHUV.md (admin qismi).

/// <summary><c>GET /api/admin/today</c> qatoridagi <c>presence</c>; null — bugun check-in yo'q yoki kuzatuv (sozlama) o'chiq.</summary>
/// <param name="State"><c>inside | outside | inaccurate | signalLost | stopped | noConsent</c> (<see cref="LocationTracking.Evaluate"/>).</param>
/// <param name="LastPingAt">Oxirgi ping vaqti (UTC); bugun ping bo'lmasa null.</param>
/// <param name="DistanceM">Oxirgi ping masofasi (butun metr); ping bo'lmasa — check-in masofasi.</param>
/// <param name="OutsideMinutes">Bugun tashqarida o'tgan vaqt (daqiqa).</param>
/// <param name="Mocked">Bugun kamida bitta soxta joylashuv (<c>isMocked</c>) ping'i bo'lganmi.</param>
public sealed record PresenceDto(
    PresenceState State,
    DateTimeOffset? LastPingAt,
    double? DistanceM,
    int OutsideMinutes,
    bool Mocked)
{
    public static PresenceDto From(PresenceSnapshot snapshot)
        => new(snapshot.State, snapshot.LastPingAt, snapshot.DistanceM is { } d ? Math.Round(d) : null,
            snapshot.OutsideMinutes, snapshot.Mocked);
}

/// <summary><c>GET /api/admin/students/{studentId}/location-pings?date=</c>.</summary>
/// <param name="Place">Shu kun davomati davrining tasdiqlangan arizasidagi korxona; davomat yoki ariza yo'q bo'lsa null.</param>
/// <param name="ConsentGiven">Talaba HOZIR fonda kuzatuvga rozilik berganmi.</param>
/// <param name="Pings">Shu kun ping'lari, <c>occurredAt</c> o'sish tartibida.</param>
public sealed record StudentLocationPingsDto(
    DateOnly Date,
    TrackingPlaceDto? Place,
    DateTimeOffset? CheckInAt,
    DateTimeOffset? CheckOutAt,
    bool ConsentGiven,
    IReadOnlyList<LocationPingDto> Pings,
    LocationPingsSummaryDto Summary);

/// <param name="RadiusM">Korxonaning hozirgi radiusi (har ping o'z paytidagi radius bilan baholangan).</param>
public sealed record TrackingPlaceDto(string Name, double Lat, double Lng, int RadiusM);

/// <param name="DistanceM">Korxonagacha masofa (butun metr).</param>
public sealed record LocationPingDto(
    DateTimeOffset OccurredAt,
    double Lat,
    double Lng,
    double AccuracyM,
    double DistanceM,
    LocationPingStatus Status,
    bool IsMocked,
    LocationPingSource Source);

/// <param name="OutsideMinutes"><see cref="LocationTracking.OutsideMinutes"/> (joriy <c>trackingIntervalSec</c> bilan).</param>
/// <param name="LongestGapMinutes">Ketma-ket ping'lar orasidagi eng uzun oraliq (daqiqa, kesilmagan); 2 tadan kam ping — 0.</param>
public sealed record LocationPingsSummaryDto(
    int Total,
    int Inside,
    int Outside,
    int Inaccurate,
    int OutsideMinutes,
    int LongestGapMinutes,
    DateTimeOffset? LastPingAt);
