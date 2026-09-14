using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.Applications;

/// <summary><c>POST /api/tutor/applications/{id}/decision</c>. Tasdiqlashda radius korxonaga yoziladi
/// (o'zgargan bo'lsa audit <c>RadiusChanged</c>). Allaqachon hal qilingan ariza → 409 (Domain).</summary>
public sealed record DecideApplicationCommand(
    Guid ApplicationId,
    ApplicationDecision Decision,
    int? RadiusM,
    IReadOnlyList<int>? Checklist,
    string? Comment) : IRequest<ApplicationDecisionResponse>;

internal sealed class DecideApplicationCommandHandler(
    IApplicationDbContext db,
    IScopeResolver scopeResolver,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock)
    : IRequestHandler<DecideApplicationCommand, ApplicationDecisionResponse>
{
    public async Task<ApplicationDecisionResponse> Handle(DecideApplicationCommand request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        var userId = currentUser.UserId ?? throw new ForbiddenException();
        var now = clock.UtcNow;

        var application = await db.PracticeApplications
            .Include(a => a.Company)
            .InScope(scope)
            .FirstOrDefaultAsync(a => a.Id == request.ApplicationId, cancellationToken)
            ?? throw new NotFoundException("Ariza", request.ApplicationId);

        switch (request.Decision)
        {
            case ApplicationDecision.Approve:
                var radius = request.RadiusM ?? application.ProposedRadiusM;
                var previousRadius = application.Company.RadiusM;
                application.Approve(userId, radius, request.Checklist ?? [], request.Comment, now);
                if (application.Company.SetRadius(radius))
                {
                    await audit.WriteAsync(
                        AuditAction.RadiusChanged, nameof(Company), application.CompanyId.ToString(),
                        changes: $"{{\"radiusM\":{{\"from\":{previousRadius},\"to\":{radius}}}}}",
                        reason: $"Ariza tasdiqlandi: {application.Id}",
                        cancellationToken: cancellationToken);
                }

                await audit.WriteAsync(
                    AuditAction.ApplicationApproved, nameof(PracticeApplication), application.Id.ToString(),
                    reason: request.Comment, cancellationToken: cancellationToken);
                break;

            case ApplicationDecision.Return:
                application.ReturnForRevision(userId, request.Comment ?? string.Empty, now);
                await audit.WriteAsync(
                    AuditAction.ApplicationReturned, nameof(PracticeApplication), application.Id.ToString(),
                    reason: request.Comment, cancellationToken: cancellationToken);
                break;

            case ApplicationDecision.Reject:
                application.Reject(userId, request.Comment ?? string.Empty, now);
                await audit.WriteAsync(
                    AuditAction.ApplicationRejected, nameof(PracticeApplication), application.Id.ToString(),
                    reason: request.Comment, cancellationToken: cancellationToken);
                break;

            default:
                throw new DomainException("Noma'lum qaror.");
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ApplicationDecisionResponse(application.Id, application.Status);
    }
}
