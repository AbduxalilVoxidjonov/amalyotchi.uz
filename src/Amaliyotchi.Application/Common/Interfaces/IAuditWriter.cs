using Amaliyotchi.Domain.Enums;

namespace Amaliyotchi.Application.Common.Interfaces;

/// <summary>Avtomatik (interceptor) audit qamrab olmaydigan hodisalar uchun:
/// kirish urinishlari, qo'lda aralashuvlar, qaror bekor qilish.</summary>
public interface IAuditWriter
{
    Task WriteAsync(
        AuditAction action,
        string entityName,
        string? entityId = null,
        string? changes = null,
        string? reason = null,
        Guid? userId = null,
        UserRole? userRole = null,
        CancellationToken cancellationToken = default);
}
