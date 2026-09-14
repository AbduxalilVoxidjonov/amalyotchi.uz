using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Enums;

namespace Amaliyotchi.Domain.Auditing;

/// <summary>O'zgarmas yozuv: kim, qachon, nimani o'zgartirdi va nima uchun.
/// Hech kim — admin ham — buni tahrirlay yoki o'chira olmaydi.</summary>
public sealed class AuditLog : BaseEntity
{
    private AuditLog() { }

    public DateTimeOffset OccurredAt { get; private set; }
    public Guid? UserId { get; private set; }
    public UserRole? UserRole { get; private set; }
    public AuditAction Action { get; private set; }
    public string EntityName { get; private set; } = string.Empty;
    public string? EntityId { get; private set; }

    /// <summary>O'zgargan maydonlar JSON ko'rinishida. Parol, token va shunga o'xshash
    /// maydonlar bu yerga hech qachon tushmaydi.</summary>
    public string? Changes { get; private set; }

    /// <summary>Qo'lda aralashuv uchun majburiy.</summary>
    public string? Reason { get; private set; }

    public string? IpAddress { get; private set; }
    public string? TraceId { get; private set; }

    public static AuditLog Record(
        AuditAction action,
        string entityName,
        string? entityId = null,
        Guid? userId = null,
        UserRole? userRole = null,
        string? changes = null,
        string? reason = null,
        string? ipAddress = null,
        string? traceId = null,
        DateTimeOffset? occurredAt = null)
        => new()
        {
            OccurredAt = occurredAt ?? DateTimeOffset.UtcNow,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            UserId = userId,
            UserRole = userRole,
            Changes = changes,
            Reason = reason,
            IpAddress = ipAddress,
            TraceId = traceId
        };
}
