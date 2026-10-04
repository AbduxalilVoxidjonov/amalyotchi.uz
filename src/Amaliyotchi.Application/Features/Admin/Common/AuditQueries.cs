using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Auditing;
using Amaliyotchi.Domain.Enums;

namespace Amaliyotchi.Application.Features.Admin.Common;

/// <summary>Kontrakt v2 <c>AuditEntry</c>: xom qiymatlar — kim (<c>userName</c>, <c>userRole</c>), amal (enum),
/// qaysi yozuv (<c>entityName</c>/<c>entityId</c>), sabab va o'zgarishlar (JSON matni). Matnni frontend yasaydi.
/// <c>userRole</c> — <c>admin</c> | <c>student</c> | <c>tutor</c> (tarixiy: tyutor roli olib tashlangan, eski yozuvlarda
/// bazada <c>2</c> qolgan — enum'da yo'q, shuning uchun satr sifatida SQL'da xaritalanadi).</summary>
public sealed record AuditEntryDto(
    Guid Id,
    DateTimeOffset At,
    AuditAction Action,
    string EntityName,
    string? EntityId,
    string? Reason,
    string? Changes,
    Guid? UserId,
    string? UserName,
    string? UserRole);

public static class AuditQueries
{
    /// <summary>Audit yozuvi + foydalanuvchi ismi (chap birlashma — tizim yozuvlarida foydalanuvchi yo'q).
    /// Tartib chaqiruvchida (<c>OrderByDescending(a => a.OccurredAt)</c> proyeksiyadan OLDIN).</summary>
    public static IQueryable<AuditEntryDto> SelectEntries(this IQueryable<AuditLog> logs, IApplicationDbContext db)
        => from a in logs
           join u in db.Users on a.UserId equals u.Id into users
           from u in users.DefaultIfEmpty()
           select new AuditEntryDto(
               a.Id,
               a.OccurredAt,
               a.Action,
               a.EntityName,
               a.EntityId,
               a.Reason,
               a.Changes,
               a.UserId,
               u != null ? u.FullName : null,
               a.UserRole == null ? null
                   : a.UserRole == UserRole.Admin ? AdminRole
                   : a.UserRole == UserRole.Student ? StudentRole
                   : LegacyTutorRole);

    public const string AdminRole = "admin";
    public const string StudentRole = "student";

    /// <summary>Tarixiy rol (bazada <c>2</c>) — faqat audit jurnalidagi eski yozuvlar uchun.</summary>
    public const string LegacyTutorRole = "tutor";
}
