using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Auditing;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Infrastructure.Persistence;

namespace Amaliyotchi.Infrastructure.Services;

public sealed class AuditWriter(AppDbContext db, ICurrentUser currentUser, IClock clock) : IAuditWriter
{
    public Task WriteAsync(
        AuditAction action,
        string entityName,
        string? entityId = null,
        string? changes = null,
        string? reason = null,
        Guid? userId = null,
        UserRole? userRole = null,
        CancellationToken cancellationToken = default)
    {
        var log = AuditLog.Record(
            action,
            entityName,
            entityId,
            userId ?? currentUser.UserId,
            userRole ?? currentUser.Role,
            changes,
            reason,
            currentUser.IpAddress,
            currentUser.TraceId,
            clock.UtcNow);

        db.AuditLogs.Add(log);
        return Task.CompletedTask;
    }
}
