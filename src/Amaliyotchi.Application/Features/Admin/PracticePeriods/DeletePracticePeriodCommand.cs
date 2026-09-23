using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Practice;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.PracticePeriods;

/// <summary><c>DELETE /api/admin/practice-periods/{id}</c> → 204. Soft delete; guruh bog'lanishlari tarix uchun qoladi
/// (o'chirilgan davr hech qayerda — ro'yxat, ustma-ust tekshiruvi, talaba oqimi — ko'rinmaydi). Topilmasa → 404;
/// davrda davomat yozuvi bo'lsa → 409 (o'rniga "Yopish").</summary>
public sealed record DeletePracticePeriodCommand(Guid Id) : IRequest;

internal sealed class DeletePracticePeriodCommandHandler(IApplicationDbContext db, IAuditWriter audit, IClock clock)
    : IRequestHandler<DeletePracticePeriodCommand>
{
    public async Task Handle(DeletePracticePeriodCommand request, CancellationToken cancellationToken)
    {
        var period = await PracticePeriodQueries.FindTrackedAsync(db, request.Id, cancellationToken);
        var hasAttendance = await db.DailyAttendances.AnyAsync(a => a.PeriodId == period.Id, cancellationToken);

        period.Delete(clock.UtcNow, hasAttendance);

        await audit.WriteAsync(
            AuditAction.PracticePeriodDeleted, nameof(PracticePeriod), period.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}
