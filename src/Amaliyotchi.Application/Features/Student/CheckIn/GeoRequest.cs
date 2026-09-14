using Amaliyotchi.Application.Common.Interfaces;
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
}

/// <summary>Koordinata chegaralari, aniqlik va <c>occurredAt</c> yangiligi: 10 daqiqadan eski yoki
/// kelajakdagi (1 daqiqa soat farqiga ruxsat) vaqt qabul qilinmaydi.</summary>
public abstract class GeoRequestValidator<T> : AbstractValidator<T>
    where T : IGeoRequest
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(1);

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
    }
}
