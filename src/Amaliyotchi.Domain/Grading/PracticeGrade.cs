using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Grading;

/// <summary>Talabaning amaliyot bahosi (davr bo'yicha bitta). Davomat va hisobot ballari saqlanmaydi —
/// ular <see cref="GradeCalculator"/> bilan o'qishda hisoblanadi; bu yerda faqat tyutor kiritadigan qism.</summary>
public sealed class PracticeGrade : AuditableEntity
{
    public const int MaxTutorPoints = GradeThresholds.TutorWeight;
    public const int MaxReferencePoints = GradeThresholds.ReferenceWeight;
    public const int ConclusionMaxLength = 2000;

    private PracticeGrade() { }

    public Guid StudentUserId { get; private set; }
    public Guid PeriodId { get; private set; }

    /// <summary>Tyutor bahosi, 0..20.</summary>
    public int? TutorPoints { get; private set; }

    /// <summary>Korxona tavsifnomasi bali, 0..10.</summary>
    public int? ReferencePoints { get; private set; }

    /// <summary>Tyutor xulosasi (portfolioga kiradi).</summary>
    public string? Conclusion { get; private set; }
    public DateTimeOffset? FinalizedAt { get; private set; }
    public Guid? FinalizedByUserId { get; private set; }

    public bool IsFinalized => FinalizedAt is not null;

    public static PracticeGrade Create(Guid studentUserId, Guid periodId)
    {
        if (studentUserId == Guid.Empty)
            throw new DomainException("Talaba ko'rsatilmagan.");
        if (periodId == Guid.Empty)
            throw new DomainException("Amaliyot davri ko'rsatilmagan.");

        return new PracticeGrade { StudentUserId = studentUserId, PeriodId = periodId };
    }

    public void SetTutorPoints(int? points)
    {
        EnsureNotFinalized();
        ValidateTutorPoints(points);
        TutorPoints = points;
    }

    public void SetReferencePoints(int? points)
    {
        EnsureNotFinalized();
        ValidateReferencePoints(points);
        ReferencePoints = points;
    }

    /// <summary>Yakunlash: xulosa majburiy, tyutor bali qo'yilgan bo'lishi shart.</summary>
    public void Finalize(string conclusion, Guid byUserId, DateTimeOffset at)
    {
        EnsureNotFinalized();
        if (TutorPoints is null)
            throw new DomainException("Yakunlashdan oldin tyutor bahosi qo'yilishi kerak.");
        if (byUserId == Guid.Empty)
            throw new DomainException("Yakunlovchi ko'rsatilmagan.");
        var trimmed = conclusion?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException("Tyutor xulosasi bo'sh bo'lishi mumkin emas.");
        if (trimmed.Length > ConclusionMaxLength)
            throw new DomainException($"Xulosa {ConclusionMaxLength} belgidan oshmasligi kerak.");

        Conclusion = trimmed;
        FinalizedAt = at;
        FinalizedByUserId = byUserId;
    }

    /// <summary>Yakunlangan bahoni qayta ochish (admin ruxsati bilan, audit <c>GradeReverted</c>).</summary>
    public void Revert()
    {
        if (!IsFinalized)
            throw new ConflictException("Baho hali yakunlanmagan.");

        FinalizedAt = null;
        FinalizedByUserId = null;
    }

    internal static void ValidateTutorPoints(int? points)
    {
        if (points is not null and (< 0 or > MaxTutorPoints))
            throw new DomainException($"Tyutor bali 0–{MaxTutorPoints} oralig'ida bo'lishi kerak.");
    }

    internal static void ValidateReferencePoints(int? points)
    {
        if (points is not null and (< 0 or > MaxReferencePoints))
            throw new DomainException($"Tavsifnoma bali 0–{MaxReferencePoints} oralig'ida bo'lishi kerak.");
    }

    private void EnsureNotFinalized()
    {
        if (IsFinalized)
            throw new ConflictException("Baho yakunlangan — o'zgartirish uchun avval qayta oching.");
    }
}
