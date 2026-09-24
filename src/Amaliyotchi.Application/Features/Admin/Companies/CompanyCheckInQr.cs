using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Companies;

/// <summary>Korxonaning check-in QR kodi: chop etiladigan satr (<c>AMLQR:1:{token}</c>) va oxirgi almashtirilgan vaqt.</summary>
public sealed record CompanyCheckInQrDto(Guid CompanyId, string CompanyName, string Payload, DateTimeOffset RotatedAt);

/// <summary><c>GET /api/admin/companies/{id}/checkin-qr</c> → 200 <see cref="CompanyCheckInQrDto"/>; yo'q → 404.</summary>
public sealed record GetCompanyCheckInQrQuery(Guid Id) : IRequest<CompanyCheckInQrDto>;

/// <summary><c>POST /api/admin/companies/{id}/checkin-qr/rotate</c> — yangi token (eski QR darhol yaroqsiz),
/// audit <see cref="AuditAction.CompanyQrRotated"/>. Yo'q → 404.</summary>
public sealed record RotateCompanyCheckInQrCommand(Guid Id) : IRequest<CompanyCheckInQrDto>;

internal sealed class GetCompanyCheckInQrQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver)
    : IRequestHandler<GetCompanyCheckInQrQuery, CompanyCheckInQrDto>
{
    public async Task<CompanyCheckInQrDto> Handle(GetCompanyCheckInQrQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        return await CompanyCheckInQrs.GetAsync(db, scope, request.Id, requireScopedStudents: false, cancellationToken);
    }
}

internal sealed class RotateCompanyCheckInQrCommandHandler(
    IApplicationDbContext db, IScopeResolver scopeResolver, IAuditWriter audit, IClock clock)
    : IRequestHandler<RotateCompanyCheckInQrCommand, CompanyCheckInQrDto>
{
    public async Task<CompanyCheckInQrDto> Handle(RotateCompanyCheckInQrCommand request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        return await CompanyCheckInQrs.RotateAsync(
            db, audit, clock, scope, request.Id, requireScopedStudents: false, cancellationToken);
    }
}

/// <summary>Admin va tyutor QR amallari uchun umumiy mantiq. Tyutor ko'lami <c>GET /api/tutor/companies/{id}</c>
/// dagidek: ko'lamda shu korxonaga tasdiqlangan arizasi bor talaba bo'lmasa — 404 (mavjudligi oshkor qilinmaydi).</summary>
internal static class CompanyCheckInQrs
{
    public static async Task<CompanyCheckInQrDto> GetAsync(
        IApplicationDbContext db, DataScope scope, Guid companyId, bool requireScopedStudents, CancellationToken cancellationToken)
    {
        var company = await LoadAsync(db, scope, companyId, requireScopedStudents, track: false, cancellationToken);
        return ToDto(company);
    }

    public static async Task<CompanyCheckInQrDto> RotateAsync(
        IApplicationDbContext db,
        IAuditWriter audit,
        IClock clock,
        DataScope scope,
        Guid companyId,
        bool requireScopedStudents,
        CancellationToken cancellationToken)
    {
        var company = await LoadAsync(db, scope, companyId, requireScopedStudents, track: true, cancellationToken);
        company.RotateCheckInQr(clock.UtcNow);

        // Token o'zi audit'ga yozilmaydi — u QR sirining o'zi.
        await audit.WriteAsync(
            AuditAction.CompanyQrRotated, nameof(Company), company.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(company);
    }

    private static async Task<Company> LoadAsync(
        IApplicationDbContext db,
        DataScope scope,
        Guid companyId,
        bool requireScopedStudents,
        bool track,
        CancellationToken cancellationToken)
    {
        var companies = track ? db.Companies : db.Companies.AsNoTracking();
        var company = await companies.FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken)
            ?? throw new NotFoundException("Korxona", companyId);

        if (requireScopedStudents)
        {
            var inScope = await db.PracticeApplications
                .AsNoTracking()
                .InScope(scope)
                .AnyAsync(a => a.CompanyId == companyId && a.Status == ApplicationStatus.Approved, cancellationToken);
            if (!inScope)
                throw new NotFoundException("Korxona", companyId);
        }

        return company;
    }

    private static CompanyCheckInQrDto ToDto(Company company)
        => new(company.Id, company.Name, company.CheckInQrPayload, company.CheckInQrRotatedAt);
}
