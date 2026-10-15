using Amaliyotchi.Application.Features.Supervision.Common;
using Amaliyotchi.Domain.Faces;

namespace Amaliyotchi.Application.Features.Faces;

/// <summary>Etalon yuz holati API'da: <c>none</c> — yuborilmagan (yoki admin bekor qilgan).</summary>
public enum StudentFaceStatus
{
    None = 0,
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

/// <summary><c>GET/POST /api/student/face</c>, admin approve/reject/reset javobi va talaba profilidagi <c>face</c> bloki.</summary>
/// <param name="Required">Sozlama <c>faceVerificationEnabled</c>: <c>true</c> bo'lsa check-in selfisi etalon bilan solishtiriladi.
/// Etalon TOPSHIRISH majburiyligi bunga bog'liq emas — <see cref="EnrollmentRequired"/>.</param>
/// <param name="PhotoUrl">Etalon rasm havolasi ("/api/files/&lt;guid&gt;") yoki null.</param>
/// <param name="RejectReason">Admin rad etgan bo'lsa — sabab.</param>
public sealed record StudentFaceDto(
    StudentFaceStatus Status,
    bool Required,
    string? PhotoUrl,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ReviewedAt,
    string? RejectReason)
{
    /// <summary>v3.34: etalon topshirish har doim majburiy (sozlamadan mustaqil) — mobil ilova birinchi kirishda
    /// (yoki etalon yo'q / rad etilgan bo'lsa) yuz topshirish darvozasini ko'rsatadi.</summary>
    public bool EnrollmentRequired => true;

    /// <summary>v3.34: talaba hozir <c>POST /api/student/face</c> yubora oladimi — faqat <c>none</c>/<c>rejected</c>.
    /// <c>pending</c>/<c>approved</c> → POST 409 (<c>code=faceAlreadyEnrolled</c>).</summary>
    public bool CanSubmit => Status is StudentFaceStatus.None or StudentFaceStatus.Rejected;

    public static StudentFaceDto From(StudentFaceEnrollment? enrollment, bool required)
        => enrollment is null
            ? new StudentFaceDto(StudentFaceStatus.None, required, null, null, null, null)
            : new StudentFaceDto(
                ToStatus(enrollment.Status),
                required,
                FileUrls.For(enrollment.PhotoFileId),
                enrollment.SubmittedAt,
                enrollment.ReviewedAt,
                enrollment.RejectReason);

    public static StudentFaceStatus ToStatus(FaceEnrollmentStatus status) => status switch
    {
        FaceEnrollmentStatus.Pending => StudentFaceStatus.Pending,
        FaceEnrollmentStatus.Approved => StudentFaceStatus.Approved,
        FaceEnrollmentStatus.Rejected => StudentFaceStatus.Rejected,
        _ => StudentFaceStatus.None
    };
}

/// <summary><c>GET /api/admin/face-enrollments</c> qatori.</summary>
public sealed record FaceEnrollmentItem(
    Guid StudentId,
    string FullName,
    string HemisId,
    string Group,
    string PhotoUrl,
    StudentFaceStatus Status,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ReviewedAt,
    string? RejectReason);

/// <summary><c>GET /api/admin/face-enrollments?status=</c> javobi.</summary>
public sealed record FaceEnrollmentList(IReadOnlyList<FaceEnrollmentItem> Items);
