using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Faculties;

/// <summary><c>PATCH /api/admin/faculties/{id}/status</c>: <c>{ isActive }</c> → 200 <see cref="FacultyDto"/>.
/// <see cref="Id"/> route'dan keladi. Topilmasa → 404. O'chirish qoidalariga ta'sir qilmaydi —
/// faol emas fakultet ro'yxatda (filtrsiz) qolaveradi.</summary>
public sealed record SetFacultyStatusCommand(Guid Id, bool IsActive) : IRequest<FacultyDto>;

internal sealed class SetFacultyStatusCommandHandler(IApplicationDbContext db, IAuditWriter audit)
    : IRequestHandler<SetFacultyStatusCommand, FacultyDto>
{
    public async Task<FacultyDto> Handle(SetFacultyStatusCommand request, CancellationToken cancellationToken)
    {
        var faculty = await db.Faculties.FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Fakultet topilmadi.");

        if (faculty.IsActive != request.IsActive)
        {
            if (request.IsActive)
                faculty.Activate();
            else
                faculty.Deactivate();

            await audit.WriteAsync(
                request.IsActive ? AuditAction.FacultyActivated : AuditAction.FacultyDeactivated,
                nameof(Faculty), faculty.Id.ToString(),
                cancellationToken: cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
        }

        return new FacultyDto(faculty.Id, faculty.Name, faculty.Code, faculty.IsActive);
    }
}
