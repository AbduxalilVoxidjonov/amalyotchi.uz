using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

/// <summary><c>PATCH /api/admin/tutors/{id}/status</c>: <c>{ isActive }</c> → 204. <see cref="Id"/> route'dan.
/// <c>false</c> → hisob faolsizlantiriladi va barcha faol refresh tokenlari bekor qilinadi (sessiya yangilanmaydi);
/// <c>true</c> → qayta faollashtiriladi. Guruh biriktiruvlariga tegilmaydi. Topilmasa → 404.</summary>
public sealed record SetTutorStatusCommand(Guid Id, bool IsActive) : IRequest;

internal sealed class SetTutorStatusCommandHandler(IApplicationDbContext db, IAuditWriter audit)
    : IRequestHandler<SetTutorStatusCommand>
{
    public async Task Handle(SetTutorStatusCommand request, CancellationToken cancellationToken)
    {
        // Deactivate() faqat yuklangan tokenlarni bekor qiladi — bekor qilinmaganlari yuklanadi.
        var tutor = await db.Users
            .Include(u => u.RefreshTokens.Where(t => t.RevokedAt == null))
            .FirstOrDefaultAsync(u => u.Id == request.Id && u.Role == UserRole.Tutor, cancellationToken)
            ?? throw new NotFoundException(TutorDetailQueries.NotFoundMessage);

        if (tutor.IsActive == request.IsActive)
            return;

        if (request.IsActive)
            tutor.Activate();
        else
            tutor.Deactivate();

        await audit.WriteAsync(
            request.IsActive ? AuditAction.TutorActivated : AuditAction.TutorDeactivated,
            nameof(User), tutor.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}
