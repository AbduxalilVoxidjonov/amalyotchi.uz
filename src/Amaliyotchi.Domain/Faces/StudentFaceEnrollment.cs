using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Faces;

/// <summary>Talabaning etalon yuz rasmi ("Yuzni tasdiqlash"): har talabaga BITTA qator (yagona faol etalon).
/// Talaba rozilik bilan selfi yuboradi → <see cref="FaceEnrollmentStatus.Pending"/>; tyutor tasdiqlaydi yoki rad etadi;
/// "reset" — qator o'chiriladi (holat "none", talaba qayta yuboradi). Tarix audit jurnalida.
/// Check-in selfisi shu <see cref="Embedding"/> bilan solishtiriladi (pending ham, approved ham).</summary>
public sealed class StudentFaceEnrollment : AuditableEntity
{
    public const int RejectReasonMaxLength = 500;

    public const string AlreadyApprovedMessage =
        "Yuz allaqachon tasdiqlangan. O'zgartirish uchun tyutoringizga murojaat qiling.";

    private StudentFaceEnrollment() { }

    public Guid StudentUserId { get; private set; }

    /// <summary>Etalon selfi (<c>StoredFileKind.FacePhoto</c>).</summary>
    public Guid PhotoFileId { get; private set; }

    /// <summary>Yuz embedding'i (SFace — 128 son). Postgres <c>real[]</c>.</summary>
    public float[] Embedding { get; private set; } = [];

    public FaceEnrollmentStatus Status { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }

    /// <summary>Talaba biometrik ma'lumotni qayta ishlashga rozilik bergan vaqt (har yuborishda yangilanadi).</summary>
    public DateTimeOffset ConsentAt { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public string? RejectReason { get; private set; }

    /// <summary>Check-in'da etalon sifatida ishlatiladimi (tyutor ko'rmagan bo'lsa ham — pending).</summary>
    public bool IsUsableReference => Status is FaceEnrollmentStatus.Pending or FaceEnrollmentStatus.Approved;

    public static StudentFaceEnrollment Submit(Guid studentUserId, Guid photoFileId, float[] embedding, DateTimeOffset now)
    {
        if (studentUserId == Guid.Empty)
            throw new DomainException("Talaba ko'rsatilmagan.");

        var enrollment = new StudentFaceEnrollment { StudentUserId = studentUserId };
        enrollment.Apply(photoFileId, embedding, now);
        return enrollment;
    }

    /// <summary>Qayta yuborish (pending'ni almashtiradi yoki rad etilgandan keyin). Tasdiqlangan bo'lsa — 409.</summary>
    public void Resubmit(Guid photoFileId, float[] embedding, DateTimeOffset now)
    {
        if (Status == FaceEnrollmentStatus.Approved)
            throw new ConflictException(AlreadyApprovedMessage);

        Apply(photoFileId, embedding, now);
    }

    /// <summary>Faqat kutilayotgan rasm tasdiqlanadi.</summary>
    public void Approve(Guid byUserId, DateTimeOffset now)
    {
        if (Status != FaceEnrollmentStatus.Pending)
            throw new ConflictException(Status == FaceEnrollmentStatus.Approved
                ? "Yuz rasmi allaqachon tasdiqlangan."
                : "Rad etilgan rasmni tasdiqlab bo'lmaydi — talaba yangi rasm yuborishi kerak.");
        if (byUserId == Guid.Empty)
            throw new DomainException("Tasdiqlovchi ko'rsatilmagan.");

        Status = FaceEnrollmentStatus.Approved;
        ReviewedAt = now;
        ReviewedByUserId = byUserId;
        RejectReason = null;
    }

    /// <summary>Kutilayotgan yoki tasdiqlangan rasmni rad etish (sabab majburiy). Allaqachon rad etilgan — 409.</summary>
    public void Reject(Guid byUserId, string reason, DateTimeOffset now)
    {
        if (Status == FaceEnrollmentStatus.Rejected)
            throw new ConflictException("Yuz rasmi allaqachon rad etilgan.");
        if (byUserId == Guid.Empty)
            throw new DomainException("Rad etuvchi ko'rsatilmagan.");
        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException("Rad etish sababi majburiy.");
        if (trimmed.Length > RejectReasonMaxLength)
            throw new DomainException($"Sabab {RejectReasonMaxLength} belgidan oshmasligi kerak.");

        Status = FaceEnrollmentStatus.Rejected;
        ReviewedAt = now;
        ReviewedByUserId = byUserId;
        RejectReason = trimmed;
    }

    private void Apply(Guid photoFileId, float[] embedding, DateTimeOffset now)
    {
        if (photoFileId == Guid.Empty)
            throw new DomainException("Rasm ko'rsatilmagan.");
        ArgumentNullException.ThrowIfNull(embedding);
        if (embedding.Length == 0)
            throw new DomainException("Yuz embedding'i bo'sh.");

        PhotoFileId = photoFileId;
        Embedding = embedding;
        Status = FaceEnrollmentStatus.Pending;
        SubmittedAt = now;
        ConsentAt = now;
        ReviewedAt = null;
        ReviewedByUserId = null;
        RejectReason = null;
    }
}
