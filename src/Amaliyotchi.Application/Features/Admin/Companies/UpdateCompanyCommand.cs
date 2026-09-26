using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Companies;

/// <summary><c>PUT /api/admin/companies/{id}</c> → 200 <see cref="CompanyDetail"/>. <c>Id</c> route'dan.
/// Topilmasa → 404; STIR boshqa korxonada band bo'lsa → 409. Radius o'zgarsa audit jurnaliga
/// <c>RadiusChanged</c> ham tushadi (geofence xulqi o'zgaradi).</summary>
public sealed record UpdateCompanyCommand(
    Guid Id,
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

internal sealed class UpdateCompanyCommandHandler(
    IApplicationDbContext db, IScopeResolver scopeResolver, IAuditWriter audit, IClock clock)
    : IRequestHandler<UpdateCompanyCommand, CompanyDetail>
{
    public async Task<CompanyDetail> Handle(UpdateCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Korxona", request.Id);

        var tin = CompanyWrite.NormalizeTin(request.Tin);
        await CompanyWrite.EnsureTinFreeAsync(db, tin, exceptId: company.Id, cancellationToken);
        var radiusM = await CompanyWrite.ResolveRadiusAsync(db, request.RadiusM, cancellationToken);

        company.ChangeTin(tin);
        company.Update(
            request.Name, request.Activity, request.Address,
            request.SupervisorName, request.SupervisorPhone, request.MentorName, request.MentorPhone);
        company.Relocate(new GeoPoint(request.Lat, request.Lng));

        if (company.SetRadius(radiusM))
        {
            await audit.WriteAsync(
                AuditAction.RadiusChanged, nameof(Company), company.Id.ToString(),
                reason: $"Radius {radiusM} m ga o'zgartirildi (korxona tahriri).",
                cancellationToken: cancellationToken);
        }

        await audit.WriteAsync(
            AuditAction.CompanyUpdated, nameof(Company), company.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        return await CompanyQueries.LoadDetailAsync(db, scope, company.Id, requireScopedStudents: false, clock.LocalToday(), cancellationToken);
    }
}
