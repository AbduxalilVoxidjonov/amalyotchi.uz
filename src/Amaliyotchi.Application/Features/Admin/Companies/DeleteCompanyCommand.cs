using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Companies;

/// <summary><c>DELETE /api/admin/companies/{id}</c> → 204 (soft delete — arxivlash).
/// Ikki qavat himoya: korxona hali FAOL bo'lsa → 409 (avval faolsizlantirish kerak),
/// unga qoralamadan boshqa arizasi bor talaba bo'lsa (tarix, istalgan davr) → 409 — davomat va
/// kundalik tarixi korxonaga bog'liq, uni yo'qotib bo'lmaydi. Xabar: hozir aktiv talabalar bo'lsa —
/// <see cref="HasStudentsMessage"/> (aktiv son bilan), aks holda <see cref="HasHistoryMessage"/>. Topilmasa → 404.</summary>
public sealed record DeleteCompanyCommand(Guid Id) : IRequest;

internal sealed class DeleteCompanyCommandHandler(IApplicationDbContext db, IAuditWriter audit, IClock clock)
    : IRequestHandler<DeleteCompanyCommand>
{
    public const string StillActiveMessage =
        "Avval korxonani faolsizlantiring — keyin o'chirish mumkin.";

    public static string HasStudentsMessage(int students) =>
        $"Korxonaga {students} ta talaba biriktirilgan — uni o'chirib bo'lmaydi. " +
        "Talabalarni boshqa korxonaga ko'chiring yoki korxonani faqat faolsizlantiring.";

    /// <summary>Hozir aktiv talaba yo'q, lekin arizalar tarixi bor — UI dagi "0 talaba" bilan zid kelmasligi uchun.</summary>
    public const string HasHistoryMessage =
        "Korxonada amaliyot tarixi (arizalar) bor — uni o'chirib bo'lmaydi, nofaol qiling.";

    public async Task Handle(DeleteCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Korxona", request.Id);

        if (company.IsActive)
            throw new ConflictException(StillActiveMessage);

        // Himoya — TARIX bo'yicha (qoralamadan boshqa istalgan ariza); xabar esa UI dagi aktiv son bilan mos.
        var attached = await CompanyWrite.CountAttachedStudentsAsync(db, company.Id, cancellationToken);
        if (attached > 0)
        {
            Guid[] companyIds = [company.Id];
            var active = (await CompanyQueries.LoadActivePlacementsAsync(
                db, scope: null, companyIds, clock.LocalToday(), cancellationToken)).Count;
            throw new ConflictException(active > 0 ? HasStudentsMessage(active) : HasHistoryMessage);
        }

        company.Delete(clock.UtcNow);

        await audit.WriteAsync(
            AuditAction.CompanyDeleted, nameof(Company), company.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}
