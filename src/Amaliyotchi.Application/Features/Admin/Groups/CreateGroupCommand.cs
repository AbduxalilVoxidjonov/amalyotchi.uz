using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Admin.Tutors;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Groups;

/// <summary><c>POST /api/admin/directions/{directionId}/groups</c>: <c>{ name, course }</c> → 201 <see cref="GroupDto"/>.
/// <see cref="DirectionId"/> route'dan keladi. O'quv yili tanlanmaydi — faol <see cref="AcademicYear"/> olinadi;
/// yo'q bo'lsa → 409. Yo'nalish topilmasa → 404. Nom (shu yo'nalishda, faol o'quv yili ichida,
/// case-insensitive) takrorlansa → 409. Yo'nalishni qamrab oluvchi faol tyutor ko'lami (fakultet/kafedra/yo'nalish)
/// bo'lsa — yangi guruh o'sha tyutorga avtomatik biriktiriladi (<see cref="TutorAssignmentSync"/>).</summary>
public sealed record CreateGroupCommand(Guid DirectionId, string Name, int Course) : IRequest<GroupDto>;

internal sealed class CreateGroupCommandHandler(IApplicationDbContext db, IAuditWriter audit)
    : IRequestHandler<CreateGroupCommand, GroupDto>
{
    public async Task<GroupDto> Handle(CreateGroupCommand request, CancellationToken cancellationToken)
    {
        var direction = await db.Directions.FirstOrDefaultAsync(d => d.Id == request.DirectionId, cancellationToken)
            ?? throw new NotFoundException("Yo'nalish topilmadi.");

        var academicYear = await db.AcademicYears.FirstOrDefaultAsync(y => y.IsActive, cancellationToken)
            ?? throw new ConflictException("Faol o'quv yili yo'q — avval o'quv yilini faollashtiring.");

        var name = request.Name.Trim();
        var exists = await db.StudentGroups.AnyAsync(
            g => g.DirectionId == request.DirectionId && g.AcademicYearId == academicYear.Id
                 && g.Name.ToLower() == name.ToLower(), cancellationToken);
        if (exists)
            throw new ConflictException($"'{name}' guruhi bu yo'nalishda allaqachon mavjud.");

        var group = direction.AddGroup(request.Name, request.Course, academicYear.Id);
        db.StudentGroups.Add(group);

        var facultyId = await db.Departments.AsNoTracking()
            .Where(d => d.Id == direction.DepartmentId)
            .Select(d => d.FacultyId)
            .FirstAsync(cancellationToken);
        await TutorAssignmentSync.AttachGroupAsync(
            db, group.Id, direction.Id, direction.DepartmentId, facultyId, academicYear.Id, cancellationToken);

        await audit.WriteAsync(
            AuditAction.GroupCreated, nameof(StudentGroup), group.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return new GroupDto(group.Id, group.DirectionId, group.Name, group.Course, group.IsActive, academicYear.Name);
    }
}
