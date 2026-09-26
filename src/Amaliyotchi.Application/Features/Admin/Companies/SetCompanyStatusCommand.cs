using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Companies;

/// <summary><c>PATCH /api/admin/companies/{id}/status</c>: <c>{ isActive }</c> → 200 <see cref="CompanyDetail"/>.
/// Faolsizlantirilgan korxona STIR qidiruvida CHIQMAYDI (<c>GET /api/companies/lookup</c> → 404), ya'ni
/// talaba uni tanlay olmaydi; mavjud arizalar va davomat esa buzilmaydi. O'chirishdan oldingi qadam.</summary>
public sealed record SetCompanyStatusCommand(Guid Id, bool IsActive) : IRequest<CompanyDetail>;

internal sealed class SetCompanyStatusCommandHandler(
    IApplicationDbContext db, IScopeResolver scopeResolver, IAuditWriter audit, IClock clock)
    : IRequestHandler<SetCompanyStatusCommand, CompanyDetail>
{
    public async Task<CompanyDetail> Handle(SetCompanyStatusCommand request, CancellationToken cancellationToken)
    {
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Korxona", request.Id);

        if (company.IsActive != request.IsActive)
        {
            if (request.IsActive)
                company.Activate();
            else
                company.Deactivate();

            await audit.WriteAsync(
                request.IsActive ? AuditAction.CompanyActivated : AuditAction.CompanyDeactivated,
                nameof(Company), company.Id.ToString(),
                cancellationToken: cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
        }

        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        return await CompanyQueries.LoadDetailAsync(db, scope, company.Id, requireScopedStudents: false, clock.LocalToday(), cancellationToken);
    }
}
