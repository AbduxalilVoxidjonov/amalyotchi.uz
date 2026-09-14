namespace Amaliyotchi.Application.Features.Tutor.Grading;

public sealed record GradingAttendance(double Points, double Pct);

public sealed record GradingReports(double Points, double Avg);

public sealed record GradingRecommended(int TutorPoints, int ReferencePoints);

/// <summary>Baholash qatori: davomat 40 + hisobotlar 30 + tyutor 20 + tavsifnoma 10 → jami → baho (2–5, null = qayta topshiradi).</summary>
public sealed record GradingRow(
    Guid StudentId,
    string Name,
    GradingAttendance Attendance,
    GradingReports Reports,
    int? TutorPoints,
    int? ReferencePoints,
    GradingRecommended Recommended,
    double Total,
    int? Grade);
