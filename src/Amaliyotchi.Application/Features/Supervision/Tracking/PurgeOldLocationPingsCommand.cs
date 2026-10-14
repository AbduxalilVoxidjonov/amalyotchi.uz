using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Domain.Attendance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Supervision.Tracking;

/// <summary>Saqlash muddati: <see cref="LocationTracking.RetentionDays"/> (60) kundan eski ping'larni jismonan o'chiradi
/// (<c>date &lt; bugun − 60</c>, Toshkent; bitta <c>DELETE</c>, indeks <c>(date, student_user_id)</c>). Fon xizmati
/// (<c>LocationPingRetentionService</c>) davriy chaqiradi; javob — o'chirilgan qatorlar soni.</summary>
public sealed record PurgeOldLocationPingsCommand : IRequest<int>;

internal sealed class PurgeOldLocationPingsCommandHandler(IApplicationDbContext db, IClock clock)
    : IRequestHandler<PurgeOldLocationPingsCommand, int>
{
    public Task<int> Handle(PurgeOldLocationPingsCommand request, CancellationToken cancellationToken)
    {
        var cutoff = clock.LocalToday().AddDays(-LocationTracking.RetentionDays);
        return db.LocationPings.Where(p => p.Date < cutoff).ExecuteDeleteAsync(cancellationToken);
    }
}
