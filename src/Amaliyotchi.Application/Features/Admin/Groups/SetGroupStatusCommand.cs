using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Admin.Tutors;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Groups;

/// <summary><c>PATCH /api/admin/groups/{id}/status</c>: <c>{ isActive }</c> → 200 <see cref="GroupDto"/>.
/// <see cref="Id"/> route'dan keladi. Topilmasa → 404. Faollashtirilganda guruhni qamrab oluvchi faol tyutor ko'lami
/// bo'lsa biriktiruv qo'shiladi/faollashadi (<see cref="TutorAssignmentSync.AttachGroupAsync"/>); faolsizlantirish
/// biriktiruvlarga tegmaydi.</summary>
public sealed record SetGroupStatusCommand(Guid Id, bool IsActive) : IRequest<GroupDto>;

internal sealed class SetGroupStatusCommandHandler(IApplicationDbContext db, IAuditWriter audit)
    : IRequestHandler<SetGroupStatusCommand, GroupDto>
{
    public async Task<GroupDto> Handle(SetGroupStatusCommand request, CancellationToken cancellationToken)
    {
        var group = await db.StudentGroups.FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Guruh topilmadi.");

        if (group.IsActive != request.IsActive)
        {
            if (request.IsActive)
            {
                group.Activate();

                var directionId = group.DirectionId;
                var chain = await (from d in db.Directions.AsNoTracking()
                                   join dept in db.Departments on d.DepartmentId equals dept.Id
                                   where d.Id == directionId
                                   select new { DirectionId = d.Id, DepartmentId = dept.Id, dept.FacultyId })
                    .FirstAsync(cancellationToken);
                await TutorAssignmentSync.AttachGroupAsync(
                    db, group.Id, chain.DirectionId, chain.DepartmentId, chain.FacultyId, group.AcademicYearId, cancellationToken);
            }
            else
            {
                group.Deactivate();
            }

            await audit.WriteAsync(
                request.IsActive ? AuditAction.GroupActivated : AuditAction.GroupDeactivated,
                nameof(StudentGroup), group.Id.ToString(),
                cancellationToken: cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
        }

        var academicYearName = await db.AcademicYears.AsNoTracking()
            .Where(y => y.Id == group.AcademicYearId)
            .Select(y => y.Name)
            .FirstAsync(cancellationToken);

        return new GroupDto(group.Id, group.DirectionId, group.Name, group.Course, group.IsActive, academicYearName);
    }
}
