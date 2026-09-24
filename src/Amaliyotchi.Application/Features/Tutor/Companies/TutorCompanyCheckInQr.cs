using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Features.Admin.Companies;
using MediatR;

namespace Amaliyotchi.Application.Features.Tutor.Companies;

/// <summary><c>GET /api/tutor/companies/{id}/checkin-qr</c> — ko'lam <c>GET /api/tutor/companies/{id}</c> dagidek:
/// ko'lamda biriktirilgan talabasi yo'q korxona → 404.</summary>
public sealed record GetTutorCompanyCheckInQrQuery(Guid Id) : IRequest<CompanyCheckInQrDto>;

/// <summary><c>POST /api/tutor/companies/{id}/checkin-qr/rotate</c> — yangi token, audit
/// <c>CompanyQrRotated</c>. Ko'lam tashqarisi → 404.</summary>
public sealed record RotateTutorCompanyCheckInQrCommand(Guid Id) : IRequest<CompanyCheckInQrDto>;

internal sealed class GetTutorCompanyCheckInQrQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver)
    : IRequestHandler<GetTutorCompanyCheckInQrQuery, CompanyCheckInQrDto>
{
    public async Task<CompanyCheckInQrDto> Handle(GetTutorCompanyCheckInQrQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        return await CompanyCheckInQrs.GetAsync(db, scope, request.Id, requireScopedStudents: true, cancellationToken);
    }
}

internal sealed class RotateTutorCompanyCheckInQrCommandHandler(
    IApplicationDbContext db, IScopeResolver scopeResolver, IAuditWriter audit, IClock clock)
    : IRequestHandler<RotateTutorCompanyCheckInQrCommand, CompanyCheckInQrDto>
{
    public async Task<CompanyCheckInQrDto> Handle(RotateTutorCompanyCheckInQrCommand request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        return await CompanyCheckInQrs.RotateAsync(
            db, audit, clock, scope, request.Id, requireScopedStudents: true, cancellationToken);
    }
}
