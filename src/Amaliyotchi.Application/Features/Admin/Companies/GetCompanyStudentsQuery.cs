using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Common.Time;
using MediatR;

namespace Amaliyotchi.Application.Features.Admin.Companies;

/// <summary><c>GET /api/admin/companies/{id}/students</c> — korxonaga ariza bergan talabalar (qoralamadan boshqa),
/// FISH bo'yicha tartiblangan. Korxona topilmasa → 404.</summary>
public sealed record GetCompanyStudentsQuery(Guid Id) : IRequest<IReadOnlyList<CompanyStudent>>;

internal sealed class GetCompanyStudentsQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver, IClock clock)
    : IRequestHandler<GetCompanyStudentsQuery, IReadOnlyList<CompanyStudent>>
{
    public async Task<IReadOnlyList<CompanyStudent>> Handle(GetCompanyStudentsQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        return await CompanyQueries.LoadStudentsAsync(
            db, scope, request.Id, requireScopedStudents: false, clock.LocalToday(), clock.LocalTime(), cancellationToken);
    }
}
