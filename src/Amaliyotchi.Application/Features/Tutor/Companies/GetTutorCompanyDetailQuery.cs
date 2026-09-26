using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Admin.Companies;
using MediatR;

namespace Amaliyotchi.Application.Features.Tutor.Companies;

/// <summary><c>GET /api/tutor/companies/{id}</c> — korxona tafsiloti (admin bilan bir xil shakl).
/// <c>students</c>, <c>suspiciousDays</c> va <c>periods</c> — ko'lamdagi AKTIV talabalar (§4.7); <c>totalStudents</c> —
/// butun tizim bo'yicha aktiv. Ko'lamda shu korxonaga hech qachon tasdiqlangan arizasi bo'lmagan korxona → 404
/// (mavjudligi oshkor qilinmaydi); tarixi bor, hozir aktivi yo'q korxona → 200, sonlar 0.</summary>
public sealed record GetTutorCompanyDetailQuery(Guid Id) : IRequest<CompanyDetail>;

internal sealed class GetTutorCompanyDetailQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver, IClock clock)
    : IRequestHandler<GetTutorCompanyDetailQuery, CompanyDetail>
{
    public async Task<CompanyDetail> Handle(GetTutorCompanyDetailQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        return await CompanyQueries.LoadDetailAsync(db, scope, request.Id, requireScopedStudents: true, clock.LocalToday(), cancellationToken);
    }
}
