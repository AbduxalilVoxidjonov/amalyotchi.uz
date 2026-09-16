using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Features.Admin.Companies;
using MediatR;

namespace Amaliyotchi.Application.Features.Tutor.Companies;

/// <summary><c>GET /api/tutor/companies/{id}</c> — korxona tafsiloti (admin bilan bir xil shakl).
/// <c>students</c>, <c>suspiciousDays</c> va <c>periods</c> — ko'lam kesimida; <c>totalStudents</c> — butun tizim.
/// Ko'lamda biriktirilgan talabasi yo'q korxona → 404 (mavjudligi oshkor qilinmaydi).</summary>
public sealed record GetTutorCompanyDetailQuery(Guid Id) : IRequest<CompanyDetail>;

internal sealed class GetTutorCompanyDetailQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver)
    : IRequestHandler<GetTutorCompanyDetailQuery, CompanyDetail>
{
    public async Task<CompanyDetail> Handle(GetTutorCompanyDetailQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        return await CompanyQueries.LoadDetailAsync(db, scope, request.Id, requireScopedStudents: true, cancellationToken);
    }
}
