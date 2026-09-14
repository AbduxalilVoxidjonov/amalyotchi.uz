using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Tutor.Common;
using MediatR;

namespace Amaliyotchi.Application.Features.Tutor.Grading;

/// <summary><c>GET /api/tutor/grading</c> — faol davrdagi ko'lam talabalari uchun baholash jadvali.</summary>
public sealed record GetGradingQuery : IRequest<IReadOnlyList<GradingRow>>;

internal sealed class GetGradingQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver, IClock clock)
    : IRequestHandler<GetGradingQuery, IReadOnlyList<GradingRow>>
{
    public async Task<IReadOnlyList<GradingRow>> Handle(GetGradingQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        var today = clock.LocalToday();
        var students = await db.LoadScopedStudentsAsync(scope, cancellationToken);
        var periods = await PeriodLookup.LoadAsync(db, today, cancellationToken);

        return await GradingRowBuilder.BuildAsync(db, scope, students, periods, today, clock.LocalTime(), cancellationToken);
    }
}
