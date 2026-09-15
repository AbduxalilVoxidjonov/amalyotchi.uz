using System.Text.Json;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

/// <summary><c>PUT /api/admin/tutors/{id}</c>: <c>{ fullName, phone?, facultyIds[] }</c> → 200 <see cref="TutorDetail"/>.
/// <see cref="Id"/> route'dan. Tyutor topilmasa → 404. Fakultetlar to'plami ALMASHTIRILADI: qo'shish erkin (yangi fakultet
/// topilmasa → 404, faol bo'lmasa → 409); OLIB TASHLANAYOTGAN fakultetda tyutorning faol ko'lami bo'lsa → 409
/// ("&lt;Fakultet&gt; fakultetida tyutorga ko'lam biriktirilgan — avval uni ajrating."). Ro'yxatning birinchisi — asosiy
/// fakultet. Telefon boshqa faol hisobda bo'lsa → 409.</summary>
public sealed record UpdateTutorCommand(Guid Id, string FullName, string? Phone, IReadOnlyList<Guid> FacultyIds) : IRequest<TutorDetail>;

internal sealed class UpdateTutorCommandHandler(IApplicationDbContext db, IAuditWriter audit)
    : IRequestHandler<UpdateTutorCommand, TutorDetail>
{
    public static string HasScopesMessage(string facultyName)
        => $"{facultyName} fakultetida tyutorga ko'lam biriktirilgan — avval uni ajrating.";

    public async Task<TutorDetail> Handle(UpdateTutorCommand request, CancellationToken cancellationToken)
    {
        var tutor = await db.Users
            .Include(u => u.Faculties)
            .FirstOrDefaultAsync(u => u.Id == request.Id && u.Role == UserRole.Tutor, cancellationToken)
            ?? throw new NotFoundException(TutorDetailQueries.NotFoundMessage);

        var facultyIds = await TutorFaculties.EnsureActiveAsync(db, request.FacultyIds, cancellationToken);

        var removed = tutor.Faculties.Select(f => f.FacultyId).Where(id => !facultyIds.Contains(id)).ToList();
        if (removed.Count > 0)
        {
            // Olib tashlanayotgan fakultetdagi faol ko'lam — 409 (nom bo'yicha birinchisi xabarda).
            var blocked = await (from s in db.TutorScopes.AsNoTracking()
                                 join f in db.Faculties on s.FacultyId equals f.Id
                                 where s.TutorUserId == tutor.Id && s.IsActive && removed.Contains(s.FacultyId)
                                 orderby f.Name
                                 select f.Name)
                .FirstOrDefaultAsync(cancellationToken);
            if (blocked is not null)
                throw new ConflictException(HasScopesMessage(blocked));
        }

        var phone = TutorPhone.NormalizeOrNull(request.Phone);
        if (phone is not null && !string.Equals(phone, tutor.PhoneNumber, StringComparison.Ordinal))
        {
            var phoneTaken = await db.Users.AnyAsync(u => u.Id != tutor.Id && u.PhoneNumber == phone, cancellationToken);
            if (phoneTaken)
                throw new ConflictException(CreateTutorCommandHandler.PhoneTakenMessage);
        }

        tutor.Rename(request.FullName);
        tutor.ChangePhoneNumber(phone);
        tutor.SetFaculties(facultyIds);

        await audit.WriteAsync(
            AuditAction.TutorUpdated, nameof(User), tutor.Id.ToString(),
            changes: JsonSerializer.Serialize(new { facultyIds }),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return await TutorDetailQueries.LoadAsync(db, tutor.Id, cancellationToken);
    }
}
