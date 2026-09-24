using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Attendance;

/// <summary>Check-in/check-out urinishi nega rad etilgani. Har urinish (rad etilgani ham) <see cref="AttendanceEvent"/> ga yoziladi.</summary>
public enum CheckInRejectReason
{
    None = 0,
    /// <summary>Ariza tasdiqlanmagan (korxona/shartnoma yo'q).</summary>
    NotApproved = 1,
    /// <summary>Dam olish kuni yoki bayram.</summary>
    NotWorkDay = 2,
    PeriodNotStarted = 3,
    PeriodEnded = 4,
    /// <summary>Oyna hali ochilmagan (09:00 dan oldin / check-out uchun 17:00 dan oldin).</summary>
    WindowNotOpen = 5,
    /// <summary>Oyna yopilgan (10:30 dan keyin / check-out uchun 18:00 dan keyin).</summary>
    WindowClosed = 6,
    PoorAccuracy = 7,
    OutOfRadius = 8,
    AlreadyCheckedIn = 9,
    /// <summary>Check-out uchun avval check-in bo'lishi kerak.</summary>
    NoCheckIn = 10,
    AlreadyCheckedOut = 11,
    /// <summary>Bu kunga tasdiqlangan ruxsat bor — davomat "sababli", belgilanish shart emas.</summary>
    OnLeave = 12,
    /// <summary>Skanerlangan QR kod talabaning amaliyot joyiga tegishli emas (yoki format noto'g'ri / eskirgan).</summary>
    QrInvalid = 13
}

public static class CheckInRejectReasonExtensions
{
    /// <summary>Foydalanuvchiga ko'rsatiladigan o'zbekcha xabar.</summary>
    public static string Message(this CheckInRejectReason reason) => reason switch
    {
        CheckInRejectReason.None => string.Empty,
        CheckInRejectReason.NotApproved => "Amaliyot joyingiz hali tasdiqlanmagan.",
        CheckInRejectReason.NotWorkDay => "Bugun ish kuni emas.",
        CheckInRejectReason.PeriodNotStarted => "Amaliyot davri hali boshlanmagan.",
        CheckInRejectReason.PeriodEnded => "Amaliyot davri tugagan.",
        CheckInRejectReason.WindowNotOpen => "Belgilanish oynasi hali ochilmagan.",
        CheckInRejectReason.WindowClosed => "Bugungi belgilanish oynasi yopilgan.",
        CheckInRejectReason.PoorAccuracy => "GPS aniqligi yetarli emas. Ochiq joyga chiqib qayta urinib ko'ring.",
        CheckInRejectReason.OutOfRadius => "Siz amaliyot joyida emassiz.",
        CheckInRejectReason.AlreadyCheckedIn => "Bugun allaqachon belgilangansiz.",
        CheckInRejectReason.NoCheckIn => "Avval kelganingizni belgilang.",
        CheckInRejectReason.AlreadyCheckedOut => "Ketish allaqachon belgilangan.",
        CheckInRejectReason.OnLeave => "Bu kunga ruxsat tasdiqlangan — belgilanish shart emas.",
        CheckInRejectReason.QrInvalid => "QR kod bu amaliyot joyiga tegishli emas.",
        _ => "Belgilanish rad etildi."
    };

    /// <summary>Rad sababini API xatosiga aylantiradi: holat ziddiyatlari (radius, QR, takror, check-in'siz) → 409,
    /// qolganlari → 400.</summary>
    public static DomainException ToException(this CheckInRejectReason reason) => reason switch
    {
        CheckInRejectReason.OutOfRadius
            or CheckInRejectReason.QrInvalid
            or CheckInRejectReason.AlreadyCheckedIn
            or CheckInRejectReason.AlreadyCheckedOut
            or CheckInRejectReason.NoCheckIn => new ConflictException(reason.Message()),
        _ => new DomainException(reason.Message())
    };
}
