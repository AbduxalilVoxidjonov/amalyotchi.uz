using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Faculties;

/// <summary><c>DELETE /api/admin/faculties/{id}</c> → 204. Soft delete: fakultet va uning yo'nalishlari
/// arxivlanadi. Topilmasa (yoki allaqachon o'chirilgan) → 404. Fakultetga biriktirilgan (o'chirilmagan)
/// foydalanuvchi yoki guruh bo'lsa → 409 — avval ularni boshqa fakultetga ko'chirish kerak.</summary>
public sealed record DeleteFacultyCommand(Guid Id) : IRequest;

internal sealed class DeleteFacultyCommandHandler(IApplicationDbContext db, IAuditWriter audit, IClock clock)
    : IRequestHandler<DeleteFacultyCommand>
{
    public async Task Handle(DeleteFacultyCommand request, CancellationToken cancellationToken)
    {
        var faculty = await db.Faculties
            .Include(f => f.Directions)
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Fakultet topilmadi.");

        var hasUsers = await db.Users.AnyAsync(u => u.FacultyId == request.Id, cancellationToken);
        var hasGroups = await (
            from d in db.Directions
            join g in db.StudentGroups on d.Id equals g.DirectionId
            where d.FacultyId == request.Id
            select g.Id).AnyAsync(cancellationToken);

        if (hasUsers || hasGroups)
            throw new ConflictException(
                "Fakultetga guruhlar, tyutorlar yoki talabalar biriktirilgan — avval ularni boshqa fakultetga ko'chiring.");

        faculty.Delete(clock.UtcNow);

        await audit.WriteAsync(
            AuditAction.FacultyDeleted, nameof(Faculty), faculty.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}
