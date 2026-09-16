using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.ValueObjects;

namespace Amaliyotchi.Domain.Attendance;

/// <summary>Har bir check-in/check-out urinishi — muvaffaqiyatsizi ham. Tyutor xaritasi va
/// anti-fraud tahlil shu yozuvlardan quriladi. O'zgarmas; audit'dan istisno (o'zi tarix).</summary>
public sealed class AttendanceEvent : BaseEntity, IAuditExempt
{
    private AttendanceEvent() { }

    public Guid StudentUserId { get; private set; }
    public Guid CompanyId { get; private set; }

    /// <summary>Toshkent kuni.</summary>
    public DateOnly Date { get; private set; }
    public AttendanceEventKind Kind { get; private set; }

    /// <summary>Qurilma vaqti (offline navbatdan kelgan bo'lishi mumkin).</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Server qabul qilgan vaqt — qaror shu vaqtga asoslanadi.</summary>
    public DateTimeOffset ReceivedAt { get; private set; }

    /// <summary>Qurilma koordinatasi.</summary>
    public GeoPoint Location { get; private set; }
    public double AccuracyM { get; private set; }
    public double DistanceM { get; private set; }

    /// <summary>Urinish paytidagi korxona radiusi (keyin o'zgarsa ham tarix saqlanadi).</summary>
    public int RadiusM { get; private set; }
    public bool Accepted { get; private set; }
    public CheckInRejectReason RejectReason { get; private set; }

    /// <summary>Urinish paytida olingan selfi (<c>StoredFileKind.CheckInPhoto</c>). Rasm yuborilmagan bo'lsa — null.
    /// Rad etilgan urinishning rasmi ham saqlanadi: tyutor shubhani shu bo'yicha tekshiradi.</summary>
    public Guid? PhotoFileId { get; private set; }

    public static AttendanceEvent Record(
        Guid studentUserId,
        Guid companyId,
        DateOnly date,
        AttendanceEventKind kind,
        DateTimeOffset occurredAt,
        DateTimeOffset receivedAt,
        GeoPoint location,
        double accuracyM,
        double distanceM,
        int radiusM,
        CheckInVerdict verdict,
        Guid? photoFileId = null)
    {
        ArgumentNullException.ThrowIfNull(verdict);
        if (studentUserId == Guid.Empty)
            throw new DomainException("Talaba ko'rsatilmagan.");

        return new AttendanceEvent
        {
            StudentUserId = studentUserId,
            CompanyId = companyId,
            Date = date,
            Kind = kind,
            OccurredAt = occurredAt,
            ReceivedAt = receivedAt,
            Location = location,
            AccuracyM = accuracyM,
            DistanceM = distanceM,
            RadiusM = radiusM,
            Accepted = verdict.Accepted,
            RejectReason = verdict.Reason,
            PhotoFileId = photoFileId
        };
    }
}
