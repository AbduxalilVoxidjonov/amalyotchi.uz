using System.Text.Json;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Domain.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

/// <summary><c>PUT /api/admin/tutors/{id}/scopes</c>: <c>{ scopes: [{ level, id }] }</c> → 200 <see cref="TutorDetail"/>.
/// <see cref="Id"/> route'dan. Faol ko'lamlar to'plamini ALMASHTIRADI: ro'yxatda bo'lmaganlar faolsizlantiriladi (tarix
/// saqlanadi), ilgari faolsizlantirilgani qaytsa — o'sha yozuv faollashadi, yangilari yaratiladi; so'ng guruh
/// biriktiruvlari <see cref="TutorAssignmentSync"/> bilan materializatsiya qilinadi. Bir tyutorning tanlovlari ichida ota
/// tanlangan bo'lsa bolalari jimgina tashlab yuboriladi.
/// Xatolar: tyutor topilmasa → 404; tugun topilmasa / faol emas / tyutor fakultetlaridan biriga tegishli emas → 400
/// ("&lt;Daraja&gt; topilmadi." / "&lt;Daraja&gt; faol emas: &lt;nom&gt;" / "&lt;Daraja&gt; tyutor fakultetiga tegishli emas: &lt;nom&gt;");
/// boshqa tyutorning faol ko'lami bilan kesishsa → 409 ("&lt;nom&gt; (&lt;daraja&gt;) &lt;FISH&gt; tyutoriga biriktirilgan.");
/// yangi biriktiruv kerak-u faol o'quv yili yo'q → 409. Tyutor faol bo'lmasa ham ruxsat — bu ma'lumot, kirish emas.</summary>
public sealed record SetTutorScopesCommand(Guid Id, IReadOnlyList<TutorScopeInput> Scopes) : IRequest<TutorDetail>;

internal sealed class SetTutorScopesCommandHandler(IApplicationDbContext db, IAuditWriter audit)
    : IRequestHandler<SetTutorScopesCommand, TutorDetail>
{
    public async Task<TutorDetail> Handle(SetTutorScopesCommand request, CancellationToken cancellationToken)
    {
        var tutor = await db.Users.AsNoTracking()
            .Where(u => u.Id == request.Id && u.Role == UserRole.Tutor)
            .Select(u => new { u.Id })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(TutorDetailQueries.NotFoundMessage);

        var facultyIds = await TutorFacultyQueries.LoadIdsAsync(db, tutor.Id, cancellationToken);
        if (facultyIds.Count == 0)
            throw new DomainException(User.FacultyRequiredMessage);

        var candidates = TutorScope.Normalize(await ResolveAsync(tutor.Id, facultyIds, request.Scopes, cancellationToken));

        await EnsureNoOverlapAsync(tutor.Id, facultyIds, candidates, cancellationToken);

        // Tyutorning barcha (faol va faolsizlantirilgan) ko'lamlari — kuzatiladi, joyida o'zgartiriladi.
        var existing = await db.TutorScopes
            .Where(s => s.TutorUserId == tutor.Id)
            .ToListAsync(cancellationToken);

        foreach (var scope in existing)
        {
            var wanted = candidates.Any(c => c.SameNode(scope));
            if (wanted && !scope.IsActive)
                scope.Activate();
            else if (!wanted && scope.IsActive)
                scope.Deactivate();
        }

        var created = candidates.Where(c => existing.All(s => !s.SameNode(c))).ToList();
        db.TutorScopes.AddRange(created);

        var active = existing.Where(s => s.IsActive).Concat(created).ToList();
        await TutorAssignmentSync.SyncAsync(db, tutor.Id, facultyIds, active, cancellationToken);

        await audit.WriteAsync(
            AuditAction.TutorScopesChanged, nameof(TutorScope), tutor.Id.ToString(),
            changes: JsonSerializer.Serialize(new
            {
                scopes = active.Select(s => new { level = JsonNamingPolicy.CamelCase.ConvertName(s.Level.ToString()), id = s.NodeId })
            }),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return await TutorDetailQueries.LoadAsync(db, tutor.Id, cancellationToken);
    }

    /// <summary>Har tanlov uchun tugun tekshiriladi (topilmadi → faol emas → tugun fakulteti tyutor fakultetlaridan biri
    /// emas) va ota id'lari denormalizatsiya qilingan <see cref="TutorScope"/> yasaladi. Tartib so'rovdagidek.</summary>
    private async Task<IReadOnlyList<TutorScope>> ResolveAsync(
        Guid tutorId, IReadOnlyCollection<Guid> tutorFacultyIds, IReadOnlyList<TutorScopeInput> inputs, CancellationToken cancellationToken)
    {
        var inputList = inputs.Distinct().ToList();
        var byLevel = inputList.ToLookup(i => i.Level, i => i.Id);

        var facultyIds = byLevel[TutorScopeLevel.Faculty].ToList();
        var faculties = facultyIds.Count == 0
            ? []
            : await db.Faculties.AsNoTracking()
                .Where(f => facultyIds.Contains(f.Id))
                .Select(f => new Node(f.Id, f.Name, f.IsActive, f.Id, null, null))
                .ToListAsync(cancellationToken);

        var departmentIds = byLevel[TutorScopeLevel.Department].ToList();
        var departments = departmentIds.Count == 0
            ? []
            : await db.Departments.AsNoTracking()
                .Where(d => departmentIds.Contains(d.Id))
                .Select(d => new Node(d.Id, d.Name, d.IsActive, d.FacultyId, d.Id, null))
                .ToListAsync(cancellationToken);

        var directionIds = byLevel[TutorScopeLevel.Direction].ToList();
        var directions = directionIds.Count == 0
            ? []
            : await (from d in db.Directions.AsNoTracking()
                     join dept in db.Departments on d.DepartmentId equals dept.Id
                     where directionIds.Contains(d.Id)
                     select new Node(d.Id, d.Name, d.IsActive, dept.FacultyId, dept.Id, d.Id))
                .ToListAsync(cancellationToken);

        var groupIds = byLevel[TutorScopeLevel.Group].ToList();
        var groups = groupIds.Count == 0
            ? []
            : await (from g in db.StudentGroups.AsNoTracking()
                     join d in db.Directions on g.DirectionId equals d.Id
                     join dept in db.Departments on d.DepartmentId equals dept.Id
                     where groupIds.Contains(g.Id)
                     select new Node(g.Id, g.Name, g.IsActive, dept.FacultyId, dept.Id, d.Id))
                .ToListAsync(cancellationToken);

        var result = new List<TutorScope>(inputList.Count);
        foreach (var input in inputList)
        {
            var nodes = input.Level switch
            {
                TutorScopeLevel.Faculty => faculties,
                TutorScopeLevel.Department => departments,
                TutorScopeLevel.Direction => directions,
                _ => groups
            };
            var title = TutorScopeQueries.LevelTitle(input.Level);

            var node = nodes.FirstOrDefault(n => n.Id == input.Id)
                ?? throw new DomainException($"{title} topilmadi.");
            if (!node.IsActive)
                throw new DomainException($"{title} faol emas: {node.Name}");
            if (!tutorFacultyIds.Contains(node.FacultyId))
                throw new DomainException($"{title} tyutor fakultetiga tegishli emas: {node.Name}");

            result.Add(TutorScope.Create(
                tutorId, input.Level, node.FacultyId, node.DepartmentId, node.DirectionId,
                input.Level == TutorScopeLevel.Group ? node.Id : null));
        }

        return result;
    }

    /// <summary>Boshqa tyutorlarning tyutor fakultetlaridagi faol ko'lamlari bilan kesishuv → 409, xabarda kesishgan
    /// BOSHQA tyutor ko'lamining nomi, darajasi va FISH.</summary>
    private async Task EnsureNoOverlapAsync(
        Guid tutorId, IReadOnlyCollection<Guid> facultyIds, IReadOnlyList<TutorScope> candidates, CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
            return;

        var others = await db.TutorScopes.AsNoTracking()
            .Where(s => s.IsActive && s.TutorUserId != tutorId && facultyIds.Contains(s.FacultyId))
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            var clash = others.FirstOrDefault(o => TutorScope.Overlaps(candidate, o));
            if (clash is null)
                continue;

            var names = await TutorScopeQueries.LoadNamesAsync(db, [clash], cancellationToken);
            var tutorName = await db.Users.AsNoTracking()
                .Where(u => u.Id == clash.TutorUserId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
            throw new ConflictException(
                $"{names[clash.Id].Name} ({TutorScopeQueries.LevelName(clash.Level)}) {tutorName} tyutoriga biriktirilgan.");
        }
    }

    private sealed record Node(Guid Id, string Name, bool IsActive, Guid FacultyId, Guid? DepartmentId, Guid? DirectionId);
}
