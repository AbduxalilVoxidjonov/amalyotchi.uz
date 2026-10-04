using System.Text.Json;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Auditing;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Amaliyotchi.Infrastructure.Persistence.Interceptors;

/// <summary>Audit va CreatedAt/UpdatedAt maydonlari shu yerda to'ldiriladi.
/// Handler yozgan odam buni eslab qolishi shart emas — bu qoidaning butun mohiyati.</summary>
public sealed class AuditSaveChangesInterceptor(ICurrentUser currentUser, IClock clock)
    : SaveChangesInterceptor
{
    /// <summary>Bu maydonlar audit yozuviga hech qachon tushmaydi.</summary>
    private static readonly HashSet<string> SensitiveProperties =
    [
        "PasswordHash", "Token", "ReplacedByToken",
        // Biometrik embedding (StudentFaceEnrollment) — audit jurnaliga tushmaydi.
        "Embedding"
    ];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            Apply(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            Apply(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    private void Apply(DbContext context)
    {
        var now = clock.UtcNow;
        var userId = currentUser.UserId;
        var logs = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AuditLog)
                continue;

            // Yuqori hajmli texnik yozuvlar (check-in urinishlari, fayl metama'lumoti, refresh token)
            // audit jurnaliga tushmaydi — ular o'zlari tarix.
            var exempt = entry.Entity is IAuditExempt;

            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity is AuditableEntity added)
                    {
                        added.CreatedAt = now;
                        added.CreatedBy = userId;
                    }

                    if (!exempt)
                        logs.Add(Build(entry, AuditAction.Created, now, userId));
                    break;

                case EntityState.Modified:
                    if (entry.Entity is AuditableEntity modified)
                    {
                        modified.UpdatedAt = now;
                        modified.UpdatedBy = userId;
                    }

                    if (!exempt)
                        logs.Add(Build(entry, AuditAction.Updated, now, userId));
                    break;

                case EntityState.Deleted:
                    // Jismoniy o'chirish o'rniga — soft delete.
                    if (entry.Entity is ISoftDeletable deletable)
                    {
                        entry.State = EntityState.Modified;
                        deletable.IsDeleted = true;
                        deletable.DeletedAt = now;
                    }

                    if (!exempt)
                        logs.Add(Build(entry, AuditAction.Deleted, now, userId));
                    break;

                case EntityState.Detached:
                case EntityState.Unchanged:
                default:
                    break;
            }
        }

        if (logs.Count > 0)
            context.Set<AuditLog>().AddRange(logs);
    }

    private AuditLog Build(EntityEntry entry, AuditAction action, DateTimeOffset now, Guid? userId)
    {
        var entityName = entry.Metadata.ClrType.Name;
        var key = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString();

        string? changes = null;
        if (action == AuditAction.Updated)
        {
            var modified = entry.Properties
                .Where(p => p.IsModified && !SensitiveProperties.Contains(p.Metadata.Name))
                .ToDictionary(
                    p => p.Metadata.Name,
                    p => new { old = p.OriginalValue?.ToString(), @new = p.CurrentValue?.ToString() });

            if (modified.Count > 0)
                changes = JsonSerializer.Serialize(modified);
        }

        return AuditLog.Record(
            action,
            entityName,
            key,
            userId,
            currentUser.Role,
            changes,
            ipAddress: currentUser.IpAddress,
            traceId: currentUser.TraceId,
            occurredAt: now);
    }
}
