using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Practice;
using MediatR;

namespace Amaliyotchi.Application.Features.Admin.PracticePeriods;

/// <summary><c>POST /api/admin/practice-periods/{id}/close</c> → 200 <see cref="PracticePeriodDetail"/> (<c>status: closed</c>).
/// Topilmasa → 404; allaqachon yopilgan → 409. Yopilgan davr talaba/tyutor oqimlaridan chiqadi (check-in to'xtaydi),
/// tarix (davomat, kundalik, baho) saqlanadi.</summary>
public sealed record ClosePracticePeriodCommand(Guid Id) : IRequest<PracticePeriodDetail>;

internal sealed class ClosePracticePeriodCommandHandler(IApplicationDbContext db, IAuditWriter audit, IClock clock)
    : IRequestHandler<ClosePracticePeriodCommand, PracticePeriodDetail>
{
    public async Task<PracticePeriodDetail> Handle(ClosePracticePeriodCommand request, CancellationToken cancellationToken)
    {
        var period = await PracticePeriodQueries.FindTrackedAsync(db, request.Id, cancellationToken);
        period.Close();

        await audit.WriteAsync(
            AuditAction.PracticePeriodClosed, nameof(PracticePeriod), period.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return await PracticePeriodQueries.LoadDetailAsync(db, clock, period.Id, cancellationToken);
    }
}
