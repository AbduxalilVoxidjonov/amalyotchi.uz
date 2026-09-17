using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Tin = Amaliyotchi.Domain.ValueObjects.Tin;

namespace Amaliyotchi.Application.Features.Companies;

/// <summary>STIR bo'yicha topilgan korxona kartasi — talaba ariza berishda faqat STIR kiritadi,
/// qolgan ma'lumot admin oldindan kiritgan yozuvdan keladi (qo'lda yozilmaydi).</summary>
public sealed record CompanyLookupDto(
    Guid Id,
    string Name,
    string Tin,
    string Activity,
    string Address,
    double Lat,
    double Lng,
    int RadiusM,
    string SupervisorName,
    string SupervisorPhone,
    string? MentorName,
    string? MentorPhone);

/// <summary><c>GET /api/companies/lookup?tin=123456789</c> — har qanday avtorizatsiyalangan foydalanuvchi uchun.
/// Faqat FAOL korxona qaytadi: faolsizlantirilgani (yoki arxivlangani) qidiruvda umuman ko'rinmaydi → 404.
/// STIR formati noto'g'ri bo'lsa → 400.</summary>
public sealed record GetCompanyByTinQuery(string Tin) : IRequest<CompanyLookupDto>;

internal sealed class GetCompanyByTinQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetCompanyByTinQuery, CompanyLookupDto>
{
    public const string NotFoundMessage =
        "Bu STIR bilan faol korxona topilmadi. Korxona avval tizimga kiritilishi kerak — tyutoringizga murojaat qiling.";

    public async Task<CompanyLookupDto> Handle(GetCompanyByTinQuery request, CancellationToken cancellationToken)
    {
        var tin = Tin.Normalize(request.Tin);

        return await db.Companies
            .AsNoTracking()
            .Where(c => c.Tin == tin && c.IsActive)
            .Select(c => new CompanyLookupDto(
                c.Id, c.Name, c.Tin, c.Activity, c.Address,
                c.Location.Latitude, c.Location.Longitude, c.RadiusM,
                c.SupervisorName, c.SupervisorPhone, c.MentorName, c.MentorPhone))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);
    }
}
