using Amaliyotchi.Application.Features.Tutor.Common;
using Amaliyotchi.Domain.Faces;

namespace Amaliyotchi.Application.Features.Faces;

/// <summary>Etalon yuz holati API'da: <c>none</c> — yuborilmagan (yoki tyutor bekor qilgan).</summary>
public enum StudentFaceStatus
{
    None = 0,
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

/// <summary><c>GET/POST /api/student/face</c>, tyutor approve/reject/reset javobi va talaba profilidagi <c>face</c> bloki.</summary>
/// <param name="Required">Sozlama <c>faceVerificationEnabled</c>: <c>true</c> bo'lsa check-in selfisi etalon bilan solishtiriladi.</param>
/// <param name="PhotoUrl">Etalon rasm havolasi ("/api/files/&lt;guid&gt;") yoki null.</param>
/// <param name="RejectReason">Tyutor rad etgan bo'lsa — sabab.</param>
public sealed record StudentFaceDto(
    StudentFaceStatus Status,
    bool Required,
    string? PhotoUrl,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ReviewedAt,
    string? RejectReason)
{
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

/// <summary><c>GET /api/tutor/face-enrollments</c> qatori.</summary>
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

/// <summary><c>GET /api/tutor/face-enrollments?status=</c> javobi.</summary>
public sealed record FaceEnrollmentList(IReadOnlyList<FaceEnrollmentItem> Items);
