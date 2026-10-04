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
    QrInvalid = 13,
    /// <summary>Yuzni tasdiqlash yoqilgan, talabaning ishlatiladigan etaloni yo'q (yubormagan yoki rad etilgan).</summary>
    FaceNotEnrolled = 14,
    /// <summary>Check-in selfisida yuz topilmadi (yoki rasmni o'qib bo'lmadi).</summary>
    FaceNotDetected = 15,
    /// <summary>Selfidagi yuz etalonga mos kelmadi (ball chegaradan past) — ball <see cref="AttendanceEvent.FaceMatchScore"/> da.</summary>
    FaceMismatch = 16
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
        CheckInRejectReason.FaceNotEnrolled => "Avval yuzingizni tasdiqlang.",
        CheckInRejectReason.FaceNotDetected => "Rasmda yuz topilmadi. Qayta suratga oling.",
        CheckInRejectReason.FaceMismatch => "Yuz etalonga mos kelmadi. Qayta suratga oling.",
        _ => "Belgilanish rad etildi."
    };

    /// <summary>Ball bilan xabar: <see cref="CheckInRejectReason.FaceMismatch"/> uchun
    /// "Yuz etalonga mos kelmadi (NN%). Qayta suratga oling."; boshqalari — <see cref="Message(CheckInRejectReason)"/>.</summary>
    public static string Message(this CheckInRejectReason reason, int? faceMatchScore)
        => reason == CheckInRejectReason.FaceMismatch && faceMatchScore is { } score
            ? $"Yuz etalonga mos kelmadi ({score.ToString(System.Globalization.CultureInfo.InvariantCulture)}%). Qayta suratga oling."
            : reason.Message();

    /// <summary>JSON'dagi nomi (camelCase): <c>faceMismatch</c>, <c>outOfRadius</c> … — ProblemDetails
    /// <c>rejectReason</c> kengaytmasi shu.</summary>
    public static string ToCode(this CheckInRejectReason reason)
    {
        var name = reason.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>Rad sababini API xatosiga aylantiradi: holat ziddiyatlari (radius, QR, takror, check-in'siz) → 409,
    /// qolganlari (yuz sabablari ham) → 400. Xatoga <c>rejectReason</c> kengaytmasi (camelCase) qo'shiladi —
    /// klient matnni emas, kodni o'qiydi.</summary>
    public static DomainException ToException(this CheckInRejectReason reason, int? faceMatchScore = null)
    {
        var message = reason.Message(faceMatchScore);
        DomainException exception = reason switch
        {
            CheckInRejectReason.OutOfRadius
                or CheckInRejectReason.QrInvalid
                or CheckInRejectReason.AlreadyCheckedIn
                or CheckInRejectReason.AlreadyCheckedOut
                or CheckInRejectReason.NoCheckIn => new ConflictException(message),
            _ => new DomainException(message)
        };
        if (reason != CheckInRejectReason.None)
            exception.Extensions[RejectReasonExtension] = reason.ToCode();
        if (faceMatchScore is { } score)
            exception.Extensions[FaceMatchScoreExtension] = score;
        return exception;
    }

    /// <summary>ProblemDetails kengaytmalari nomlari.</summary>
    public const string RejectReasonExtension = "rejectReason";
    public const string FaceMatchScoreExtension = "faceMatchScore";
}
