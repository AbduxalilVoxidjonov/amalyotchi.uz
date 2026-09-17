using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Companies;

/// <summary><c>DELETE /api/admin/companies/{id}</c> → 204 (soft delete — arxivlash).
/// Ikki qavat himoya: korxona hali FAOL bo'lsa → 409 (avval faolsizlantirish kerak),
/// unga talaba biriktirilgan bo'lsa (qoralamadan boshqa arizasi bor) → 409 — davomat va
/// kundalik tarixi korxonaga bog'liq, uni yo'qotib bo'lmaydi. Topilmasa → 404.</summary>
public sealed record DeleteCompanyCommand(Guid Id) : IRequest;

internal sealed class DeleteCompanyCommandHandler(IApplicationDbContext db, IAuditWriter audit, IClock clock)
    : IRequestHandler<DeleteCompanyCommand>
{
    public const string StillActiveMessage =
        "Avval korxonani faolsizlantiring — keyin o'chirish mumkin.";

    public static string HasStudentsMessage(int students) =>
        $"Korxonaga {students} ta talaba biriktirilgan — uni o'chirib bo'lmaydi. " +
        "Talabalarni boshqa korxonaga ko'chiring yoki korxonani faqat faolsizlantiring.";

    public async Task Handle(DeleteCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Korxona", request.Id);

        if (company.IsActive)
            throw new ConflictException(StillActiveMessage);

        var students = await CompanyWrite.CountAttachedStudentsAsync(db, company.Id, cancellationToken);
        if (students > 0)
            throw new ConflictException(HasStudentsMessage(students));

        company.Delete(clock.UtcNow);

        await audit.WriteAsync(
            AuditAction.CompanyDeleted, nameof(Company), company.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}
