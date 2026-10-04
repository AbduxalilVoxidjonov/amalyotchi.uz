using Amaliyotchi.Application.Common.Interfaces;
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

internal sealed class GetCompanyCheckInQrQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetCompanyCheckInQrQuery, CompanyCheckInQrDto>
{
    public Task<CompanyCheckInQrDto> Handle(GetCompanyCheckInQrQuery request, CancellationToken cancellationToken)
        => CompanyCheckInQrs.GetAsync(db, request.Id, cancellationToken);
}

internal sealed class RotateCompanyCheckInQrCommandHandler(
    IApplicationDbContext db, IAuditWriter audit, IClock clock)
    : IRequestHandler<RotateCompanyCheckInQrCommand, CompanyCheckInQrDto>
{
    public Task<CompanyCheckInQrDto> Handle(RotateCompanyCheckInQrCommand request, CancellationToken cancellationToken)
        => CompanyCheckInQrs.RotateAsync(db, audit, clock, request.Id, cancellationToken);
}

/// <summary>Korxona check-in QR amallari uchun umumiy mantiq (admin). Korxona topilmasa — 404.</summary>
internal static class CompanyCheckInQrs
{
    public static async Task<CompanyCheckInQrDto> GetAsync(
        IApplicationDbContext db, Guid companyId, CancellationToken cancellationToken)
    {
        var company = await LoadAsync(db, companyId, track: false, cancellationToken);
        return ToDto(company);
    }

    public static async Task<CompanyCheckInQrDto> RotateAsync(
        IApplicationDbContext db,
        IAuditWriter audit,
        IClock clock,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var company = await LoadAsync(db, companyId, track: true, cancellationToken);
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
        Guid companyId,
        bool track,
        CancellationToken cancellationToken)
    {
        var companies = track ? db.Companies : db.Companies.AsNoTracking();
        var company = await companies.FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken)
            ?? throw new NotFoundException("Korxona", companyId);


        return company;
    }

    private static CompanyCheckInQrDto ToDto(Company company)
        => new(company.Id, company.Name, company.CheckInQrPayload, company.CheckInQrRotatedAt);
}
