using Amaliyotchi.Domain.Attendance;

namespace Amaliyotchi.Application.Features.Student.Tracking;

// Live joylashuv — kontrakt docs/LIVE-JOYLASHUV.md (talaba qismi).

/// <summary><c>today.tracking</c> va <c>POST /api/student/location-pings</c> javobidagi <c>tracking</c> — doim keladi.</summary>
/// <param name="Enabled">Sozlama <c>trackingEnabled</c> yoqilgan VA sessiya faol (bugun check-in bor, check-out yo'q,
/// avtomatik yopilish vaqti o'tmagan). Rozilikka bog'liq emas — u <see cref="ConsentGiven"/> da.</param>
/// <param name="ConsentGiven">Talaba fonda kuzatuvga rozilik berganmi (<c>PUT /api/student/tracking/consent</c>).</param>
/// <param name="IntervalSec">Sozlama <c>trackingIntervalSec</c>.</param>
/// <param name="DistanceFilterM">Sozlama <c>trackingDistanceFilterM</c>.</param>
/// <param name="ActiveUntil">Sessiya ko'pi bilan qachongacha (avtomatik yopilish vaqti, UTC); sessiya faol bo'lmasa null.</param>
public sealed record TrackingDto(
    bool Enabled,
    bool ConsentGiven,
    int IntervalSec,
    int DistanceFilterM,
    DateTimeOffset? ActiveUntil);

/// <summary><c>POST /api/student/location-pings</c> so'rovidagi bitta ping.</summary>
/// <param name="Source"><c>foreground</c> | <c>background</c>; berilmasa — <c>background</c>.</param>
public sealed record LocationPingInput(
    double Lat,
    double Lng,
    double AccuracyM,
    DateTimeOffset OccurredAt,
    bool IsMocked = false,
    LocationPingSource? Source = null);

/// <summary>Oxirgi saqlangan ping (bugun).</summary>
public sealed record LastPingDto(LocationPingStatus Status, double DistanceM, DateTimeOffset OccurredAt);

/// <summary><c>POST /api/student/location-pings</c> javobi.</summary>
/// <param name="Accepted">Shu so'rovda saqlangan ping'lar soni (tashlanganlari — eskirgan/kelajakdagi, takror,
/// sessiyadan tashqari, rozilik yo'q, kuzatuv o'chiq — kirmaydi).</param>
/// <param name="Last">Talabaning bugungi oxirgi ping'i (shu so'rovdagisi bo'lishi shart emas); yo'q bo'lsa null.</param>
public sealed record LocationPingsResultDto(int Accepted, TrackingDto Tracking, LastPingDto? Last);
