using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Admin.Companies;
using MediatR;

namespace Amaliyotchi.Application.Features.Tutor.Companies;

/// <summary><c>GET /api/tutor/companies/{id}/students</c> — shu korxonaga ariza bergan, ko'lamdagi talabalar.
/// Ko'lamda biriktirilgan talabasi yo'q korxona → 404.</summary>
public sealed record GetTutorCompanyStudentsQuery(Guid Id) : IRequest<IReadOnlyList<CompanyStudent>>;

internal sealed class GetTutorCompanyStudentsQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver, IClock clock)
    : IRequestHandler<GetTutorCompanyStudentsQuery, IReadOnlyList<CompanyStudent>>
{
    public async Task<IReadOnlyList<CompanyStudent>> Handle(GetTutorCompanyStudentsQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        return await CompanyQueries.LoadStudentsAsync(
            db, scope, request.Id, requireScopedStudents: true, clock.LocalToday(), clock.LocalTime(), cancellationToken);
    }
}
