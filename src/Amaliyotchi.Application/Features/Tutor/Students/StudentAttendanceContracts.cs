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

/// <summary>Kundagi bitta check-in/check-out urinishi (qabul qilingan yoki rad etilgan) — tyutor/admin
/// "talaba qanday rasmga tushdi" ni ko'rishi uchun.</summary>
/// <param name="At">"HH:mm" (Toshkent) — <see cref="AttendancePunch.At"/> bilan bir xil manba (server qabul qilgan vaqt).</param>
/// <param name="AtIso">Server qabul qilgan vaqt (<c>ReceivedAt</c>, Toshkent offset bilan).</param>
/// <param name="RejectReason">Rad sababi; qabul qilinganda null.</param>
/// <param name="RejectMessage">Rad sababining o'zbekcha matni; qabul qilinganda null.</param>
/// <param name="RadiusM">Urinish paytidagi korxona radiusi.</param>
/// <param name="PhotoUrl">Selfie havolasi ("/api/files/&lt;guid&gt;") yoki null.</param>
public sealed record StudentAttendanceEvent(
    Guid Id,
    AttendanceEventKind Kind,
    string At,
    DateTimeOffset AtIso,
    bool Accepted,
    CheckInRejectReason? RejectReason,
    string? RejectMessage,
    double DistanceM,
    double AccuracyM,
    int RadiusM,
    double Lat,
    double Lng,
    string? PhotoUrl);

/// <summary>Kunga tegishli kundalik yozuvi (bo'lsa).</summary>
public sealed record StudentAttendanceDiary(Guid Id, DiaryStatus Status, int? Score);

/// <summary>Talabaning bir kunlik davomati — bazada qatori bo'lmagan kunlar ham shu shaklda qaytariladi
/// (holat <c>AttendanceStatusResolver</c> bilan hisoblanadi).</summary>
/// <param name="Attempts">Shu kundagi check-in urinishlari soni (<c>AttendanceEvent</c>).</param>
/// <param name="RejectedAttempts">Ulardan rad etilganlari.</param>
/// <param name="Events">Shu kundagi barcha check-in/check-out urinishlari vaqt bo'yicha o'sish tartibida (bo'lmasa — bo'sh).</param>
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
    int RejectedAttempts,
    IReadOnlyList<StudentAttendanceEvent> Events);
