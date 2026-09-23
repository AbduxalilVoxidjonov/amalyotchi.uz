using Amaliyotchi.Application.Common.Interfaces;
using MediatR;

namespace Amaliyotchi.Application.Features.Admin.PracticePeriods;

/// <summary><c>GET /api/admin/practice-periods/{id}</c> → <see cref="PracticePeriodDetail"/>. Topilmasa (yoki o'chirilgan) → 404.</summary>
public sealed record GetPracticePeriodQuery(Guid Id) : IRequest<PracticePeriodDetail>;

internal sealed class GetPracticePeriodQueryHandler(IApplicationDbContext db, IClock clock)
    : IRequestHandler<GetPracticePeriodQuery, PracticePeriodDetail>
{
    public Task<PracticePeriodDetail> Handle(GetPracticePeriodQuery request, CancellationToken cancellationToken)
        => PracticePeriodQueries.LoadDetailAsync(db, clock, request.Id, cancellationToken);
}
