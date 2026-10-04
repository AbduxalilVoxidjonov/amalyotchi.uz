namespace Amaliyotchi.Domain.Faces;

/// <summary>Talaba etalon yuz rasmining holati. Raqamlar bazada saqlanadi — o'zgartirilmaydi.
/// "Yuborilmagan" (none) holati qator yo'qligi bilan ifodalanadi.</summary>
public enum FaceEnrollmentStatus
{
    /// <summary>Talaba yubordi, admin hali ko'rmagan. Check-in'da etalon sifatida ISHLATILADI.</summary>
    Pending = 1,

    /// <summary>Admin tasdiqladi — talaba o'zi almashtira olmaydi (faqat admin "reset" qiladi).</summary>
    Approved = 2,

    /// <summary>Admin rad etdi (sabab bilan) — talaba qayta yuborishi kerak; check-in'da ishlatilmaydi.</summary>
    Rejected = 3
}
