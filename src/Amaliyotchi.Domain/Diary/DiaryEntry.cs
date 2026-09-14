using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Diary;

/// <summary>Talabaning kunlik hisoboti. Bir kunga bitta yozuv. Tyutor ko'radi, 1–5 ball qo'yadi yoki
/// qayta yozishga qaytaradi. Minimal matn uzunligi sozlamadan (<c>minReportLength</c>) keladi.</summary>
public sealed class DiaryEntry : AuditableEntity
{
    public const int DefaultMinTextLength = 150;
    public const int TextMaxLength = 10_000;
    public const int MaxAttachments = 5;
    public const int MinScore = 1;
    public const int MaxScore = 5;
    public const int CommentMaxLength = 1000;

    private readonly List<DiaryAttachment> _attachments = [];

    private DiaryEntry() { }

    public Guid StudentUserId { get; private set; }
    public Guid PeriodId { get; private set; }

    /// <summary>Hisobot qaysi kun uchun (Toshkent).</summary>
    public DateOnly Date { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    public string Text { get; private set; } = string.Empty;

    /// <summary>"Bugun nimani o'rgandim" — ixtiyoriy, baholashda hisobga olinadi.</summary>
    public string? Learned { get; private set; }
    public DiaryStatus Status { get; private set; }
    public int? Score { get; private set; }
    public string? TutorComment { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    public IReadOnlyCollection<DiaryAttachment> Attachments => _attachments.AsReadOnly();

    public static DiaryEntry Create(
        Guid studentUserId,
        Guid periodId,
        DateOnly date,
        string text,
        string? learned,
        DateTimeOffset submittedAt,
        int minTextLength = DefaultMinTextLength)
    {
        if (studentUserId == Guid.Empty)
            throw new DomainException("Talaba ko'rsatilmagan.");
        if (periodId == Guid.Empty)
            throw new DomainException("Amaliyot davri ko'rsatilmagan.");

        var entry = new DiaryEntry
        {
            StudentUserId = studentUserId,
            PeriodId = periodId,
            Date = date,
            SubmittedAt = submittedAt,
            Status = DiaryStatus.Submitted
        };
        entry.SetContent(text, learned, minTextLength);
        return entry;
    }

    public DiaryAttachment AddAttachment(Guid storedFileId, string fileName, long sizeBytes)
    {
        if (_attachments.Count >= MaxAttachments)
            throw new DomainException($"Bitta hisobotga ko'pi bilan {MaxAttachments} ta fayl biriktiriladi.");
        if (_attachments.Any(a => a.StoredFileId == storedFileId))
            throw new ConflictException("Bu fayl allaqachon biriktirilgan.");

        var attachment = DiaryAttachment.Create(Id, storedFileId, fileName, sizeBytes);
        _attachments.Add(attachment);
        return attachment;
    }

    /// <summary>Qayta yozishga qaytarilgan hisobotni talaba tuzatib qayta yuboradi.</summary>
    public void Resubmit(string text, string? learned, DateTimeOffset at, int minTextLength = DefaultMinTextLength)
    {
        if (Status != DiaryStatus.Rewrite)
            throw new ConflictException("Faqat qayta yozishga qaytarilgan hisobotni qayta yuborish mumkin.");

        SetContent(text, learned, minTextLength);
        SubmittedAt = at;
        Status = DiaryStatus.Submitted;
        Score = null;
        ReviewedByUserId = null;
        ReviewedAt = null;
    }

    /// <summary>Tyutor ochib ko'rdi — faqat <c>Submitted</c> holatidan; boshqa holatlarda o'zgarish yo'q.</summary>
    public void MarkSeen(Guid byUserId, DateTimeOffset at)
    {
        if (Status != DiaryStatus.Submitted)
            return;

        Status = DiaryStatus.Seen;
        ReviewedByUserId = byUserId;
        ReviewedAt = at;
    }

    /// <summary>Tasdiqlash — ball ixtiyoriy (1–5), izoh ixtiyoriy.</summary>
    public void Approve(Guid byUserId, int? score, string? comment, DateTimeOffset at)
    {
        EnsureReviewable("tasdiqlab");
        if (score is not null and (< MinScore or > MaxScore))
            throw new DomainException($"Ball {MinScore}–{MaxScore} oralig'ida bo'lishi kerak.");

        Score = score;
        Review(DiaryStatus.Approved, byUserId, comment, at);
    }

    /// <summary>Qayta yozishga qaytarish — izoh majburiy.</summary>
    public void RequestRewrite(Guid byUserId, string comment, DateTimeOffset at)
    {
        EnsureReviewable("qaytarib");
        if (string.IsNullOrWhiteSpace(comment))
            throw new DomainException("Qayta yozish sababi (izoh) majburiy.");

        Score = null;
        Review(DiaryStatus.Rewrite, byUserId, comment, at);
    }

    private void EnsureReviewable(string verb)
    {
        if (Status is not (DiaryStatus.Submitted or DiaryStatus.Seen))
            throw new ConflictException($"Hisobot allaqachon ko'rib chiqilgan — uni {verb} bo'lmaydi (holat: {Status}).");
    }

    private void Review(DiaryStatus status, Guid byUserId, string? comment, DateTimeOffset at)
    {
        if (byUserId == Guid.Empty)
            throw new DomainException("Tekshiruvchi ko'rsatilmagan.");
        var trimmed = comment?.Trim();
        if (trimmed is { Length: > CommentMaxLength })
            throw new DomainException($"Izoh {CommentMaxLength} belgidan oshmasligi kerak.");

        Status = status;
        TutorComment = string.IsNullOrEmpty(trimmed) ? null : trimmed;
        ReviewedByUserId = byUserId;
        ReviewedAt = at;
    }

    private void SetContent(string text, string? learned, int minTextLength)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length < minTextLength)
            throw new DomainException($"Hisobot matni kamida {minTextLength} belgidan iborat bo'lishi kerak.");
        if (trimmed.Length > TextMaxLength)
            throw new DomainException($"Hisobot matni {TextMaxLength} belgidan oshmasligi kerak.");

        Text = trimmed;
        Learned = string.IsNullOrWhiteSpace(learned) ? null : learned.Trim();
    }
}
