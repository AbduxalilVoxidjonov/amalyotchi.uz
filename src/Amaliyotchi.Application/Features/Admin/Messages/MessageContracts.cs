using Amaliyotchi.Domain.Messaging;

namespace Amaliyotchi.Application.Features.Admin.Messages;

/// <summary>Telegram ulangan talaba — xabar qabul qiluvchisi. <paramref name="UserId"/> — <c>User.Id</c>.
/// <paramref name="BotBlocked"/> — bot unga oxirgi marta yoza olmagan (<c>User.TelegramBotBlockedAt</c>); yuborishga
/// to'sqinlik qilmaydi (talaba botni qayta ishga tushirgan bo'lishi mumkin). Akademik maydonlar — profil/guruh zanjiri
/// bo'lmasa null.</summary>
public sealed record MessageRecipientRow(
    Guid UserId,
    string FullName,
    string? HemisId,
    long TelegramUserId,
    DateTimeOffset? TelegramLinkedAt,
    bool BotBlocked,
    string? FacultyName,
    string? DirectionName,
    string? GroupName,
    int? Course);

/// <summary>Guruh tanlagichi varianti (faqat Telegram ulangan talabasi bor guruhlar).</summary>
public sealed record MessageRecipientGroupOption(Guid Id, string Name);

/// <summary>Xabar tarixi qatori va bitta xabar. Sonlar yetkazishlardan GROUP BY bilan hisoblanadi:
/// <c>total = sent + failed + blocked + pending</c>. <see cref="Status"/> — <see cref="BroadcastStatusRule"/>.</summary>
public sealed record MessageSummary(
    Guid Id,
    string Text,
    DateTimeOffset CreatedAt,
    string CreatedByName,
    string AudienceLabel,
    bool AttachAppButton,
    BroadcastMessageStatus Status,
    int Total,
    int Sent,
    int Failed,
    int Blocked,
    int Pending);

/// <summary>Bitta talabaga yetkazish. <paramref name="Error"/> — o'zbekcha qisqa sabab (failed/blocked, yoki
/// qayta urinish kutilayotgan pending).</summary>
public sealed record MessageDeliveryRow(
    Guid UserId,
    string FullName,
    string? HemisId,
    BroadcastDeliveryStatus Status,
    string? Error,
    DateTimeOffset? SentAt);

/// <summary>Auditoriya: <c>{ kind: "selected", userIds }</c> | <c>{ kind: "filter", q?, facultyId?, directionId?, groupId?, course? }</c>
/// | <c>{ kind: "all" }</c>. Boshqa turdagi maydonlar e'tiborga olinmaydi.</summary>
public sealed record MessageAudienceInput
{
    public BroadcastAudienceKind Kind { get; init; }
    public IReadOnlyList<Guid>? UserIds { get; init; }
    public string? Q { get; init; }
    public Guid? FacultyId { get; init; }
    public Guid? DirectionId { get; init; }
    public Guid? GroupId { get; init; }
    public int? Course { get; init; }
}
