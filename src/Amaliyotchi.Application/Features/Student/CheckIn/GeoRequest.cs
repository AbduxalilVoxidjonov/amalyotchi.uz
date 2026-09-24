using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Files;
using FluentValidation;

namespace Amaliyotchi.Application.Features.Student.CheckIn;

/// <summary>Qurilma joylashuvi (kontrakt <c>CheckinRequest</c>): check-in va check-out bir xil so'rov.</summary>
public interface IGeoRequest
{
    double Lat { get; }
    double Lng { get; }

    /// <summary>GPS aniqligi, metr.</summary>
    double Accuracy { get; }

    /// <summary>Qurilma vaqti (ISO 8601). Server vaqtidan ancha farq qilsa — 400.</summary>
    DateTimeOffset OccurredAt { get; }

    /// <summary>Urinish paytidagi selfi (multipart <c>photo</c>). Ixtiyoriy — sozlama
    /// <c>checkinPhotoRequired</c> yoqilgan bo'lsa majburiy.</summary>
    UploadedFile? Photo { get; }

    /// <summary>Amaliyot joyida skanerlangan QR satri (<c>AMLQR:1:{token}</c>). Ixtiyoriy — sozlama
    /// <c>checkinQrRequired</c> yoqilgan bo'lsa majburiy. Yuborilgan bo'lsa DOIM tekshiriladi: korxona tokeniga
    /// mos kelmasa urinish <c>QrInvalid</c> bilan rad etiladi (409).</summary>
    string? Qr { get; }
}

/// <summary>Koordinata chegaralari, aniqlik va <c>occurredAt</c> yangiligi: 10 daqiqadan eski yoki
/// kelajakdagi (1 daqiqa soat farqiga ruxsat) vaqt qabul qilinmaydi. Rasm qoidalari kundalik fayllari
/// bilan bir xil (5 MB, rasm turlari) — xatolar <c>errors.Photo</c> da. Rasmning MAJBURIYligi
/// (<c>checkinPhotoRequired</c>) bu yerda emas — sozlama <see cref="AttendanceAttempt"/> da tekshiriladi.</summary>
public abstract class GeoRequestValidator<T> : AbstractValidator<T>
    where T : IGeoRequest
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(1);

    /// <summary>Selfi hajmi chegarasi — kundalik ilovalari bilan bir xil.</summary>
    public const long MaxPhotoSizeBytes = 5 * 1024 * 1024;

    /// <summary>QR satri chegarasi — haqiqiy payload 40 belgi; uzun satr format tekshiruvigacha kesiladi.</summary>
    public const int MaxQrLength = 256;

    /// <summary>Faqat rasm: kundalik ilovalaridan farqli, PDF qabul qilinmaydi.</summary>
    public static readonly IReadOnlySet<string> AllowedPhotoContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/heic", "image/heif"
    };

    protected GeoRequestValidator(IClock clock)
    {
        RuleFor(x => x.Lat)
            .Must(v => !double.IsNaN(v) && v is >= -90 and <= 90)
            .WithMessage("Kenglik (lat) -90 va 90 oralig'ida bo'lishi kerak.");

        RuleFor(x => x.Lng)
            .Must(v => !double.IsNaN(v) && v is >= -180 and <= 180)
            .WithMessage("Uzunlik (lng) -180 va 180 oralig'ida bo'lishi kerak.");

        RuleFor(x => x.Accuracy)
            .Must(v => !double.IsNaN(v) && v >= 0 && v <= 100_000)
            .WithMessage("GPS aniqligi (accuracy) 0 dan katta metr qiymati bo'lishi kerak.");

        RuleFor(x => x.OccurredAt)
            .Must(at => at <= clock.UtcNow + FutureTolerance)
            .WithMessage("Qurilma vaqti kelajakda — telefon soatini tekshiring.")
            .Must(at => at >= clock.UtcNow - MaxAge)
            .WithMessage("Urinish vaqti eskirgan (10 daqiqadan ko'p) — qayta urinib ko'ring.");

        RuleFor(x => x.Qr)
            .Must(q => q is null || q.Length <= MaxQrLength)
            .WithMessage("QR kod satri juda uzun.");

        When(x => x.Photo is not null, () =>
        {
            RuleFor(x => x.Photo!)
                .Must(p => p.Length > 0 && p.Length <= MaxPhotoSizeBytes)
                .WithMessage("Rasm 5 MB dan oshmasligi va bo'sh bo'lmasligi kerak.")
                .Must(p => AllowedPhotoContentTypes.Contains(p.ContentType))
                .WithMessage("Faqat rasm (JPEG, PNG, WebP, HEIC) qabul qilinadi.")
                .Must(p => !string.IsNullOrWhiteSpace(p.FileName) && p.FileName.Trim().Length <= StoredFile.FileNameMaxLength)
                .WithMessage("Rasm nomi bo'sh yoki juda uzun.");
        });
    }
}
