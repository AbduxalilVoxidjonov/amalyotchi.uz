using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Diary;

namespace Amaliyotchi.Application.Features.Tutor.Students;

/// <summary>Bitta check-in yoki check-out belgisi.</summary>
/// <param name="At">"HH:mm" (Toshkent).</param>
/// <param name="AtIso">To'liq ISO vaqt (offset bilan).</param>
/// <param name="Lat">Qurilma koordinatasi — <c>AttendanceEvent.Location</c> dan (qator bo'lsa ham hodisasiz kunda null).</param>
/// <param name="PhotoUrl">Selfie havolasi ("/api/files/&lt;guid&gt;") yoki null.</param>
/// <param name="OutOfRadius">Masofa korxona geofence radiusidan katta.</param>
public sealed record AttendancePunch(
    string At,
    DateTimeOffset AtIso,
    double? DistanceM,
    double? AccuracyM,
    double? Lat,
    double? Lng,
    string? PhotoUrl,
    bool OutOfRadius);

/// <summary>Kunga tegishli kundalik yozuvi (bo'lsa).</summary>
public sealed record StudentAttendanceDiary(Guid Id, DiaryStatus Status, int? Score);

/// <summary>Talabaning bir kunlik davomati — bazada qatori bo'lmagan kunlar ham shu shaklda qaytariladi
/// (holat <c>AttendanceStatusResolver</c> bilan hisoblanadi).</summary>
/// <param name="Attempts">Shu kundagi check-in urinishlari soni (<c>AttendanceEvent</c>).</param>
/// <param name="RejectedAttempts">Ulardan rad etilganlari.</param>
public sealed record StudentAttendanceDay(
    DateOnly Date,
    AttendanceStatus Status,
    bool IsWorkDay,
    AttendancePunch? CheckIn,
    AttendancePunch? CheckOut,
    bool AutoClosed,
    bool Suspicious,
    string? SuspiciousReason,
    bool Manual,
    string? ManualReason,
    Guid? LeaveRequestId,
    StudentAttendanceDiary? Diary,
    int Attempts,
    int RejectedAttempts);
