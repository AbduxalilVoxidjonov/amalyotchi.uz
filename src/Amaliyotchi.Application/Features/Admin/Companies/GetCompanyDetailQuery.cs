using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using MediatR;

namespace Amaliyotchi.Application.Features.Admin.Companies;

/// <summary><c>GET /api/admin/companies/{id}</c> — korxona tafsiloti, davrlar kesimi va STIR nazorati bilan.
/// Topilmasa (yoki o'chirilgan bo'lsa) → 404.</summary>
public sealed record GetCompanyDetailQuery(Guid Id) : IRequest<CompanyDetail>;

internal sealed class GetCompanyDetailQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver)
    : IRequestHandler<GetCompanyDetailQuery, CompanyDetail>
{
    public async Task<CompanyDetail> Handle(GetCompanyDetailQuery request, CancellationToken cancellationToken)
    {
        // Admin ko'lami cheklanmagan — shu bois `students` = `totalStudents`, ko'lam sharti ham qo'yilmaydi.
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        return await CompanyQueries.LoadDetailAsync(db, scope, request.Id, requireScopedStudents: false, cancellationToken);
    }
}
