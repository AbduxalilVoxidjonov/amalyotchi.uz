using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.ValueObjects;
using MediatR;

namespace Amaliyotchi.Application.Features.Admin.Companies;

/// <summary><c>POST /api/admin/companies</c> → 201 <see cref="CompanyDetail"/>. Admin korxonani
/// OLDINDAN yaratadi: talaba keyin faqat STIR kiritadi va shu yozuv chiqadi (<c>GET /api/companies/lookup</c>).
/// STIR band bo'lsa → 409. <c>radiusM</c> berilmasa <c>geofenceRadius</c> sozlamasidan olinadi.</summary>
public sealed record CreateCompanyCommand(
    string Name,
    string Tin,
    string Activity,
    string Address,
    double Lat,
    double Lng,
    int? RadiusM,
    string SupervisorName,
    string SupervisorPhone,
    string? MentorName,
    string? MentorPhone) : IRequest<CompanyDetail>;

internal sealed class CreateCompanyCommandHandler(
    IApplicationDbContext db, IScopeResolver scopeResolver, IAuditWriter audit, IClock clock)
    : IRequestHandler<CreateCompanyCommand, CompanyDetail>
{
    public async Task<CompanyDetail> Handle(CreateCompanyCommand request, CancellationToken cancellationToken)
    {
        var tin = CompanyWrite.NormalizeTin(request.Tin);
        await CompanyWrite.EnsureTinFreeAsync(db, tin, exceptId: null, cancellationToken);
        var radiusM = await CompanyWrite.ResolveRadiusAsync(db, request.RadiusM, cancellationToken);

        var company = Company.Create(
            request.Name, tin, request.Activity, request.Address,
            new GeoPoint(request.Lat, request.Lng), radiusM,
            request.SupervisorName, request.SupervisorPhone, request.MentorName, request.MentorPhone, clock.UtcNow);

        db.Companies.Add(company);

        await audit.WriteAsync(
            AuditAction.CompanyCreated, nameof(Company), company.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        return await CompanyQueries.LoadDetailAsync(db, scope, company.Id, requireScopedStudents: false, clock.LocalToday(), cancellationToken);
    }
}
