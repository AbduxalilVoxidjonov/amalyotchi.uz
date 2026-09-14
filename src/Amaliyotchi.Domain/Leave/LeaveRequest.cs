using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Leave;

/// <summary>Talabaning ruxsat so'rovi (kasallik, oilaviy sabab). Tasdiqlansa oraliqdagi ish kunlari
/// "sababli" bo'ladi — buni handler <c>DailyAttendance.Excuse</c> orqali qiladi.</summary>
public sealed class LeaveRequest : AuditableEntity
{
    public const int MinReasonLength = 10;
    public const int ReasonMaxLength = 1000;
    public const int CommentMaxLength = 1000;
    /// <summary>Bitta so'rov bilan so'raladigan maksimal kunlar (haddan tashqari uzun so'rovlarga to'siq).</summary>
    public const int MaxDays = 31;

    private LeaveRequest() { }

    public Guid StudentUserId { get; private set; }
    public Guid PeriodId { get; private set; }
    public DateOnly DateFrom { get; private set; }
    public DateOnly DateTo { get; private set; }
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Hujjat nomi (fayl yuklash keyingi bosqichda — hozircha faqat nom).</summary>
    public string? AttachmentName { get; private set; }
    public Guid? DocumentFileId { get; private set; }
    public LeaveRequestStatus Status { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public string? DecisionComment { get; private set; }

    public int DayCount => DateTo.DayNumber - DateFrom.DayNumber + 1;

    public bool Covers(DateOnly date) => date >= DateFrom && date <= DateTo;

    public static LeaveRequest Create(
        Guid studentUserId,
        Guid periodId,
        DateOnly dateFrom,
        DateOnly dateTo,
        string reason,
        string? attachmentName = null,
        Guid? documentFileId = null)
    {
        if (studentUserId == Guid.Empty)
            throw new DomainException("Talaba ko'rsatilmagan.");
        if (periodId == Guid.Empty)
            throw new DomainException("Amaliyot davri ko'rsatilmagan.");
        if (dateTo < dateFrom)
            throw new DomainException("Tugash sanasi boshlanish sanasidan oldin bo'lishi mumkin emas.");
        if (dateTo.DayNumber - dateFrom.DayNumber + 1 > MaxDays)
            throw new DomainException($"Bitta so'rov ko'pi bilan {MaxDays} kunni qamrab oladi.");

        var trimmedReason = reason?.Trim() ?? string.Empty;
        if (trimmedReason.Length < MinReasonLength)
            throw new DomainException($"Sabab kamida {MinReasonLength} belgidan iborat bo'lishi kerak.");
        if (trimmedReason.Length > ReasonMaxLength)
            throw new DomainException($"Sabab {ReasonMaxLength} belgidan oshmasligi kerak.");

        return new LeaveRequest
        {
            StudentUserId = studentUserId,
            PeriodId = periodId,
            DateFrom = dateFrom,
            DateTo = dateTo,
            Reason = trimmedReason,
            AttachmentName = string.IsNullOrWhiteSpace(attachmentName) ? null : attachmentName.Trim(),
            DocumentFileId = documentFileId,
            Status = LeaveRequestStatus.Pending
        };
    }

    public void Approve(Guid byUserId, string? comment, DateTimeOffset at)
        => Decide(LeaveRequestStatus.Approved, byUserId, comment, at);

    public void Reject(Guid byUserId, string? comment, DateTimeOffset at)
        => Decide(LeaveRequestStatus.Rejected, byUserId, comment, at);

    private void Decide(LeaveRequestStatus status, Guid byUserId, string? comment, DateTimeOffset at)
    {
        if (Status != LeaveRequestStatus.Pending)
            throw new ConflictException($"So'rov allaqachon hal qilingan (holat: {Status}).");
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
}
