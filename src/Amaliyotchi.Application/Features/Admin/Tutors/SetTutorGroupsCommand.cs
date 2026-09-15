using System.Text.Json;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

/// <summary><c>PUT /api/admin/tutors/{id}/groups</c>: <c>{ groupIds }</c> → 200 <see cref="TutorDetail"/>. <see cref="Id"/> route'dan.
/// To'plamni ALMASHTIRADI: ro'yxatda bo'lmagan faol biriktiruvlar faolsizlantiriladi (tarix saqlanadi), yangilari faol
/// o'quv yili bilan yaratiladi, ilgari faolsizlantirilgani qayta kelsa — mavjud yozuv faollashtiriladi.
/// Xatolar: tyutor topilmasa → 404; guruh topilmasa → 404; guruh faol emas / tyutor fakultetiga tegishli emas → 400;
/// guruh boshqa faol tyutorda → 409; yangi biriktiruv kerak-u faol o'quv yili yo'q → 409.
/// Tyutor faol bo'lmasa ham ruxsat — bu ma'lumot, kirish emas.</summary>
public sealed record SetTutorGroupsCommand(Guid Id, IReadOnlyList<Guid> GroupIds) : IRequest<TutorDetail>;

internal sealed class SetTutorGroupsCommandHandler(IApplicationDbContext db, IAuditWriter audit)
    : IRequestHandler<SetTutorGroupsCommand, TutorDetail>
{
    public const string NoActiveAcademicYearMessage = "Faol o'quv yili yo'q.";

    public async Task<TutorDetail> Handle(SetTutorGroupsCommand request, CancellationToken cancellationToken)
    {
        var tutor = await db.Users.AsNoTracking()
            .Where(u => u.Id == request.Id && u.Role == UserRole.Tutor)
            .Select(u => new { u.Id, u.FacultyId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(TutorDetailQueries.NotFoundMessage);

        var requested = request.GroupIds.Distinct().ToList();

        var groups = requested.Count == 0
            ? []
            : await (from g in db.StudentGroups.AsNoTracking()
                     join d in db.Directions on g.DirectionId equals d.Id
                     join dept in db.Departments on d.DepartmentId equals dept.Id
                     where requested.Contains(g.Id)
                     select new { g.Id, g.Name, g.IsActive, dept.FacultyId })
                .ToListAsync(cancellationToken);

        foreach (var groupId in requested)
        {
            var group = groups.FirstOrDefault(g => g.Id == groupId)
                ?? throw new NotFoundException("Guruh topilmadi.");
            if (!group.IsActive)
                throw new DomainException($"Guruh faol emas: {group.Name}");
            if (group.FacultyId != tutor.FacultyId)
                throw new DomainException($"Guruh tyutor fakultetiga tegishli emas: {group.Name}");
        }

        if (requested.Count > 0)
        {
            var takenByOther = await (from a in db.TutorAssignments.AsNoTracking()
                                      join u in db.Users on a.TutorUserId equals u.Id
                                      where a.IsActive && a.TutorUserId != tutor.Id && requested.Contains(a.StudentGroupId)
                                      select new { a.StudentGroupId, TutorName = u.FullName })
                .FirstOrDefaultAsync(cancellationToken);
            if (takenByOther is not null)
            {
                var groupName = groups.First(g => g.Id == takenByOther.StudentGroupId).Name;
                throw new ConflictException($"{groupName} guruhi {takenByOther.TutorName} tyutoriga biriktirilgan.");
            }
        }

        // Tyutorning barcha (faol va faolsizlantirilgan) biriktiruvlari — kuzatiladi, joyida o'zgartiriladi.
        var existing = await db.TutorAssignments
            .Where(a => a.TutorUserId == tutor.Id)
            .ToListAsync(cancellationToken);

        foreach (var assignment in existing)
        {
            var wanted = requested.Contains(assignment.StudentGroupId);
            if (wanted && !assignment.IsActive)
                assignment.Activate();
            else if (!wanted && assignment.IsActive)
                assignment.Deactivate();
        }

        var toCreate = requested.Where(id => existing.All(a => a.StudentGroupId != id)).ToList();
        if (toCreate.Count > 0)
        {
            var academicYearId = await db.AcademicYears.AsNoTracking()
                .Where(y => y.IsActive)
                .Select(y => (Guid?)y.Id)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new ConflictException(NoActiveAcademicYearMessage);

            foreach (var groupId in toCreate)
                db.TutorAssignments.Add(TutorAssignment.Create(tutor.Id, groupId, academicYearId));
        }

        await audit.WriteAsync(
            AuditAction.TutorGroupsChanged, nameof(TutorAssignment), tutor.Id.ToString(),
            changes: JsonSerializer.Serialize(new { groupIds = requested }),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return await TutorDetailQueries.LoadAsync(db, tutor.Id, cancellationToken);
    }
}
