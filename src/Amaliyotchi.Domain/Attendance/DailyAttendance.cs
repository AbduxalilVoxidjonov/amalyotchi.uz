using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Attendance;

/// <summary>Talabaning bir kunlik davomati. Faqat hodisa bo'lgan kunlar uchun qator yaratiladi:
/// check-in, ruxsat, qo'lda tuzatish. Absent/DayOff/Pending o'qishda hisoblanadi.
/// Vaqtlar UTC; <see cref="Date"/> — Toshkent kuni.</summary>
public sealed class DailyAttendance : AuditableEntity
{
    public const int ReasonMaxLength = 500;

    private DailyAttendance() { }

    public Guid StudentUserId { get; private set; }
    public Guid PeriodId { get; private set; }
    public DateOnly Date { get; private set; }
    public AttendanceStatus Status { get; private set; }
    public DateTimeOffset? CheckInAt { get; private set; }
    public double? CheckInDistanceM { get; private set; }
    public double? CheckInAccuracyM { get; private set; }
    public DateTimeOffset? CheckOutAt { get; private set; }
    public double? CheckOutDistanceM { get; private set; }

    /// <summary>Qabul qilingan check-in selfisi (<c>StoredFileKind.CheckInPhoto</c>); rasm yuborilmagan bo'lsa — null.</summary>
    public Guid? CheckInPhotoFileId { get; private set; }

    /// <summary>Qabul qilingan check-out selfisi; rasm yuborilmagan bo'lsa — null.</summary>
    public Guid? CheckOutPhotoFileId { get; private set; }

    /// <summary>Check-in selfisi va etalon yuz o'xshashligi (0–100); yuzni tasdiqlash o'chiq bo'lgan kunlarda — null.</summary>
    public int? FaceMatchScore { get; private set; }

    /// <summary>Check-out qilinmagani uchun tizim avtomatik yopgan.</summary>
    public bool AutoClosed { get; private set; }

    /// <summary>Soxta lokatsiya belgisi — avtomatik jazolamaydi, faqat tyutorga ko'rsatiladi.</summary>
    public bool IsSuspicious { get; private set; }
    public string? SuspiciousReason { get; private set; }

    /// <summary>Tyutor qo'lda kiritgan/tuzatgan (sabab bilan).</summary>
    public bool IsManual { get; private set; }
    public string? ManualReason { get; private set; }
    public Guid? ManualByUserId { get; private set; }

    /// <summary>Sababli holat qaysi ruxsatdan kelgani.</summary>
    public Guid? LeaveRequestId { get; private set; }

    public bool HasCheckedIn => CheckInAt is not null;
    public bool HasCheckedOut => CheckOutAt is not null;

    /// <summary>Qabul qilingan check-in'dan yozuv yaratadi. Rad etilgan verdict bilan chaqirish — xato:
    /// rad etilgan urinish faqat <see cref="AttendanceEvent"/> ga yoziladi.</summary>
    public static DailyAttendance CheckIn(
        Guid studentUserId, Guid periodId, DateOnly date, DateTimeOffset at, double distanceM, double accuracyM,
        CheckInVerdict verdict, Guid? photoFileId = null)
    {
        ArgumentNullException.ThrowIfNull(verdict);
        if (!verdict.Accepted)
            throw new DomainException("Rad etilgan urinish davomatga yozilmaydi.");

        var attendance = New(studentUserId, periodId, date);
        attendance.Status = verdict.Status;
        attendance.CheckInAt = at;
        attendance.CheckInDistanceM = distanceM;
        attendance.CheckInAccuracyM = accuracyM;
        attendance.CheckInPhotoFileId = photoFileId;
        attendance.FaceMatchScore = verdict.FaceMatchScore;
        return attendance;
    }

    public void CheckOut(DateTimeOffset at, double distanceM, Guid? photoFileId = null)
    {
        if (!HasCheckedIn)
            throw new ConflictException(CheckInRejectReason.NoCheckIn.Message());
        if (HasCheckedOut || AutoClosed)
            throw new ConflictException(CheckInRejectReason.AlreadyCheckedOut.Message());
        if (at < CheckInAt)
            throw new DomainException("Ketish vaqti kelish vaqtidan oldin bo'lishi mumkin emas.");

        CheckOutAt = at;
        CheckOutDistanceM = distanceM;
        CheckOutPhotoFileId = photoFileId;
    }

    /// <summary>Tasdiqlangan ruxsat uchun "sababli" kun yaratadi.</summary>
    public static DailyAttendance Excuse(Guid studentUserId, Guid periodId, DateOnly date, Guid leaveRequestId)
    {
        var attendance = New(studentUserId, periodId, date);
        attendance.MarkExcused(leaveRequestId);
        return attendance;
    }

    /// <summary>Mavjud kunni "sababli" qiladi (ruxsat check-in'dan keyin tasdiqlansa ham).</summary>
    public void MarkExcused(Guid leaveRequestId)
    {
        if (leaveRequestId == Guid.Empty)
            throw new DomainException("Ruxsat so'rovi ko'rsatilmagan.");

        Status = AttendanceStatus.Excused;
        LeaveRequestId = leaveRequestId;
    }

    /// <summary>Tyutor qo'lda kiritgan kun (sabab majburiy, audit <c>ManualCheckIn</c>).</summary>
    public static DailyAttendance Manual(
        Guid studentUserId, Guid periodId, DateOnly date, AttendanceStatus status, Guid byUserId, string reason, DateTimeOffset at)
    {
        var attendance = New(studentUserId, periodId, date);
        attendance.ManualFix(status, byUserId, reason, at);
        return attendance;
    }

    /// <summary>Tyutor holatni qo'lda tuzatadi. Faqat Present/Late/Absent/Excused — Pending/DayOff saqlanmaydi.</summary>
    public void ManualFix(AttendanceStatus status, Guid byUserId, string reason, DateTimeOffset at)
    {
        if (status is AttendanceStatus.Pending or AttendanceStatus.DayOff)
            throw new DomainException("Qo'lda faqat keldi / kech keldi / kelmadi / sababli holatlari qo'yiladi.");
        if (byUserId == Guid.Empty)
            throw new DomainException("Tuzatuvchi ko'rsatilmagan.");
        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException("Qo'lda tuzatish sababi majburiy.");
        if (trimmed.Length > ReasonMaxLength)
            throw new DomainException($"Sabab {ReasonMaxLength} belgidan oshmasligi kerak.");

        Status = status;
        IsManual = true;
        ManualReason = trimmed;
        ManualByUserId = byUserId;
        if (status is AttendanceStatus.Present or AttendanceStatus.Late)
            CheckInAt ??= at;
        IsSuspicious = false;
        SuspiciousReason = null;
    }

    public void MarkSuspicious(string reason)
    {
        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException("Shubha sababi bo'sh bo'lishi mumkin emas.");

        IsSuspicious = true;
        SuspiciousReason = trimmed;
    }

    public void ClearSuspicious()
    {
        IsSuspicious = false;
        SuspiciousReason = null;
    }

    /// <summary>Check-out bo'lmagan kunni tizim yopadi (18:00). Check-in bo'lmagan kunga qo'llanmaydi.</summary>
    public void AutoClose(DateTimeOffset at)
    {
        if (!HasCheckedIn || HasCheckedOut || AutoClosed)
            return;

        CheckOutAt = at;
        AutoClosed = true;
    }

    private static DailyAttendance New(Guid studentUserId, Guid periodId, DateOnly date)
    {
        if (studentUserId == Guid.Empty)
            throw new DomainException("Talaba ko'rsatilmagan.");
        if (periodId == Guid.Empty)
            throw new DomainException("Amaliyot davri ko'rsatilmagan.");

        return new DailyAttendance { StudentUserId = studentUserId, PeriodId = periodId, Date = date };
    }
}
