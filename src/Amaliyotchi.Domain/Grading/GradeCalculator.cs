using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Grading;

/// <summary>Baholash chegaralari (FUNKSIONAL-QOLLANMA §12). Hozircha konstanta, keyin sozlamaga ko'chirish mumkin.</summary>
public static class GradeThresholds
{
    public const int AttendanceWeight = 40;
    public const int ReportWeight = 30;
    public const int TutorWeight = 20;
    public const int ReferenceWeight = 10;

    /// <summary>Jami ≥ 86 → 5, ≥ 71 → 4, ≥ 56 → 3, aks holda 2.</summary>
    public const double ExcellentFrom = 86;
    public const double GoodFrom = 71;
    public const double SatisfactoryFrom = 56;

    /// <summary>Davomat shundan past bo'lsa amaliyot qayta topshiriladi — baho qo'yilmaydi.</summary>
    public const double MinAttendancePct = 70;

    public const int MaxDiaryScore = 5;
}

/// <summary>Hisoblangan ballar. <see cref="Grade"/> null — davomat yetarli emas (qayta topshiradi).</summary>
/// <param name="AttendancePoints">0..40</param>
/// <param name="ReportPoints">0..30</param>
/// <param name="TutorPoints">0..20 (tyutor qo'ymagan bo'lsa 0)</param>
/// <param name="ReferencePoints">0..10 (bo'lmasa 0)</param>
/// <param name="Total">Ballar yig'indisi, 0..100</param>
/// <param name="Grade">2..5 yoki null</param>
/// <param name="RecommendedTutorPoints">Kundalik o'rtacha balidan tavsiya</param>
/// <param name="RecommendedReferencePoints">Davomat foizidan tavsiya</param>
public sealed record GradeResult(
    double AttendancePoints,
    double ReportPoints,
    int TutorPoints,
    int ReferencePoints,
    double Total,
    int? Grade,
    int RecommendedTutorPoints,
    int RecommendedReferencePoints)
{
    public bool MustRetake => Grade is null;
}

/// <summary>Sof hisob: davomat 40% + hisobotlar 30% + tyutor 20% + tavsifnoma 10% → 100 ball → baho 2–5.</summary>
public static class GradeCalculator
{
    public static GradeResult Compute(
        double attendancePct, int diaryCount, double diaryAvg, int? tutorPoints, int? referencePoints)
    {
        if (double.IsNaN(attendancePct) || attendancePct is < 0 or > 100)
            throw new DomainException("Davomat foizi 0–100 oralig'ida bo'lishi kerak.");
        if (diaryCount < 0)
            throw new DomainException("Hisobotlar soni manfiy bo'lishi mumkin emas.");
        if (diaryCount > 0 && (double.IsNaN(diaryAvg) || diaryAvg is < 0 or > GradeThresholds.MaxDiaryScore))
            throw new DomainException($"Hisobot o'rtacha bali 0–{GradeThresholds.MaxDiaryScore} oralig'ida bo'lishi kerak.");
        PracticeGrade.ValidateTutorPoints(tutorPoints);
        PracticeGrade.ValidateReferencePoints(referencePoints);

        var attendancePoints = Round1(attendancePct / 100 * GradeThresholds.AttendanceWeight);
        var reportPoints = diaryCount == 0
            ? 0
            : Round1(diaryAvg / GradeThresholds.MaxDiaryScore * GradeThresholds.ReportWeight);
        var tutor = tutorPoints ?? 0;
        var reference = referencePoints ?? 0;
        var total = Round1(attendancePoints + reportPoints + tutor + reference);

        int? grade = attendancePct < GradeThresholds.MinAttendancePct ? null : GradeFor(total);

        var recommendedTutor = diaryCount == 0
            ? 0
            : (int)Math.Round(diaryAvg / GradeThresholds.MaxDiaryScore * GradeThresholds.TutorWeight, MidpointRounding.AwayFromZero);
        var recommendedReference = (int)Math.Round(attendancePct / 100 * GradeThresholds.ReferenceWeight, MidpointRounding.AwayFromZero);

        return new GradeResult(
            attendancePoints, reportPoints, tutor, reference, total, grade, recommendedTutor, recommendedReference);
    }

    public static int GradeFor(double total) => total switch
    {
        >= GradeThresholds.ExcellentFrom => 5,
        >= GradeThresholds.GoodFrom => 4,
        >= GradeThresholds.SatisfactoryFrom => 3,
        _ => 2
    };

    private static double Round1(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);
}
