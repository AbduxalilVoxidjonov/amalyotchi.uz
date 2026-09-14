using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Practice;

/// <summary>Talabaning amaliyot joyi arizasi: korxona, shartnoma fayli, taklif qilingan radius va tyutor qarori.
/// Faqat <c>Approved</c> ariza check-in'ga ruxsat beradi.</summary>
public sealed class PracticeApplication : AuditableEntity
{
    /// <summary>Tyutor tekshiruv ro'yxati punktlari soni (indekslar 0..6).</summary>
    public const int ChecklistItemCount = 7;
    public const int CommentMaxLength = 1000;

    private PracticeApplication() { }

    public Guid StudentUserId { get; private set; }
    public Guid PeriodId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Company Company { get; private set; } = null!;
    public ApplicationStatus Status { get; private set; }

    /// <summary>Talaba taklif qilgan radius; tasdiqlashda tyutor o'z qiymatini beradi (Company'ga yoziladi).</summary>
    public int ProposedRadiusM { get; private set; }
    public Guid? ContractFileId { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public string? DecisionComment { get; private set; }

    /// <summary>Tyutor belgilagan tekshiruv punktlari indekslari (0..6). Npgsql <c>integer[]</c>.</summary>
    public int[] Checklist { get; private set; } = [];

    /// <summary>Necha marta qayta ishlashga qaytarilgan.</summary>
    public int RevisionCount { get; private set; }

    public bool IsDecided => Status is ApplicationStatus.Approved or ApplicationStatus.Rejected or ApplicationStatus.Completed;

    public static PracticeApplication Create(
        Guid studentUserId, Guid periodId, Guid companyId, int proposedRadiusM, Guid? contractFileId, DateTimeOffset submittedAt)
    {
        if (studentUserId == Guid.Empty)
            throw new DomainException("Talaba ko'rsatilmagan.");
        if (periodId == Guid.Empty)
            throw new DomainException("Amaliyot davri ko'rsatilmagan.");
        if (companyId == Guid.Empty)
            throw new DomainException("Korxona ko'rsatilmagan.");
        ValidateRadius(proposedRadiusM);

        return new PracticeApplication
        {
            StudentUserId = studentUserId,
            PeriodId = periodId,
            CompanyId = companyId,
            ProposedRadiusM = proposedRadiusM,
            ContractFileId = contractFileId,
            SubmittedAt = submittedAt,
            Status = ApplicationStatus.Submitted
        };
    }

    /// <summary>Tasdiqlash. Radius korxonaga <c>Company.SetRadius</c> orqali handler'da yoziladi.</summary>
    public void Approve(Guid byUserId, int radiusM, IEnumerable<int> checklist, string? comment, DateTimeOffset at)
    {
        EnsureAwaitingDecision("tasdiqlab");
        ValidateRadius(radiusM);

        Checklist = NormalizeChecklist(checklist);
        ProposedRadiusM = radiusM;
        Decide(ApplicationStatus.Approved, byUserId, comment, at);
    }

    /// <summary>Qayta ishlashga qaytarish — izoh majburiy (talaba nimani tuzatishini bilishi kerak).</summary>
    public void ReturnForRevision(Guid byUserId, string comment, DateTimeOffset at)
    {
        EnsureAwaitingDecision("qaytarib");
        if (string.IsNullOrWhiteSpace(comment))
            throw new DomainException("Qaytarish sababi (izoh) majburiy.");

        RevisionCount++;
        Decide(ApplicationStatus.RevisionNeeded, byUserId, comment, at);
    }

    /// <summary>Rad etish — izoh majburiy.</summary>
    public void Reject(Guid byUserId, string comment, DateTimeOffset at)
    {
        EnsureAwaitingDecision("rad etib");
        if (string.IsNullOrWhiteSpace(comment))
            throw new DomainException("Rad etish sababi (izoh) majburiy.");

        Decide(ApplicationStatus.Rejected, byUserId, comment, at);
    }

    /// <summary>Talaba tuzatib qayta yuboradi — faqat <c>RevisionNeeded</c> holatidan.</summary>
    public void Resubmit(Guid companyId, int proposedRadiusM, Guid? contractFileId, DateTimeOffset at)
    {
        if (Status != ApplicationStatus.RevisionNeeded)
            throw new ConflictException("Faqat qayta ishlashga qaytarilgan arizani qayta yuborish mumkin.");
        if (companyId == Guid.Empty)
            throw new DomainException("Korxona ko'rsatilmagan.");
        ValidateRadius(proposedRadiusM);

        CompanyId = companyId;
        ProposedRadiusM = proposedRadiusM;
        ContractFileId = contractFileId;
        SubmittedAt = at;
        DecidedAt = null;
        DecidedByUserId = null;
        DecisionComment = null;
        Status = ApplicationStatus.Submitted;
    }

    /// <summary>Amaliyot yakunlangach (davr yopilib, baho qo'yilgach).</summary>
    public void Complete()
    {
        if (Status != ApplicationStatus.Approved)
            throw new ConflictException("Faqat tasdiqlangan arizani yakunlash mumkin.");
        Status = ApplicationStatus.Completed;
    }

    private void EnsureAwaitingDecision(string verb)
    {
        if (Status != ApplicationStatus.Submitted)
            throw new ConflictException($"Ariza allaqachon hal qilingan — uni {verb} bo'lmaydi (holat: {Status}).");
    }

    private void Decide(ApplicationStatus status, Guid byUserId, string? comment, DateTimeOffset at)
    {
        if (byUserId == Guid.Empty)
            throw new DomainException("Qaror qabul qiluvchi ko'rsatilmagan.");
        var trimmed = comment?.Trim();
        if (trimmed is { Length: > CommentMaxLength })
            throw new DomainException($"Izoh {CommentMaxLength} belgidan oshmasligi kerak.");

        Status = status;
        DecidedByUserId = byUserId;
        DecidedAt = at;
        DecisionComment = string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static void ValidateRadius(int radiusM)
    {
        if (radiusM is < Company.MinRadiusM or > Company.MaxRadiusM)
            throw new DomainException($"Radius {Company.MinRadiusM}–{Company.MaxRadiusM} m oralig'ida bo'lishi kerak.");
    }

    private static int[] NormalizeChecklist(IEnumerable<int> checklist)
    {
        ArgumentNullException.ThrowIfNull(checklist);
        var items = checklist.Distinct().Order().ToArray();
        if (items.Any(i => i < 0 || i >= ChecklistItemCount))
            throw new DomainException($"Tekshiruv ro'yxati indekslari 0–{ChecklistItemCount - 1} oralig'ida bo'lishi kerak.");
        return items;
    }
}
