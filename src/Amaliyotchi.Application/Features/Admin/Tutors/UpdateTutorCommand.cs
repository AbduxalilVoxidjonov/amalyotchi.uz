using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

/// <summary><c>PUT /api/admin/tutors/{id}</c>: <c>{ fullName, phone?, facultyId }</c> → 200 <see cref="TutorDetail"/>.
/// <see cref="Id"/> route'dan. Tyutor topilmasa → 404. Fakultet o'zgarsa: yangi fakultet topilmasa → 404, faol bo'lmasa → 409,
/// tyutorda faol guruh biriktiruvlari bo'lsa → 409 (avval ajratish kerak — guruhlar eski fakultetda qoladi).
/// Telefon boshqa faol hisobda bo'lsa → 409.</summary>
public sealed record UpdateTutorCommand(Guid Id, string FullName, string? Phone, Guid FacultyId) : IRequest<TutorDetail>;

internal sealed class UpdateTutorCommandHandler(IApplicationDbContext db, IAuditWriter audit)
    : IRequestHandler<UpdateTutorCommand, TutorDetail>
{
    public const string HasAssignmentsMessage = "Tyutorga guruhlar biriktirilgan — avval ularni ajrating.";

    public async Task<TutorDetail> Handle(UpdateTutorCommand request, CancellationToken cancellationToken)
    {
        var tutor = await db.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id && u.Role == UserRole.Tutor, cancellationToken)
            ?? throw new NotFoundException(TutorDetailQueries.NotFoundMessage);

        if (tutor.FacultyId != request.FacultyId)
        {
            var faculty = await db.Faculties.AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == request.FacultyId, cancellationToken)
                ?? throw new NotFoundException("Fakultet topilmadi.");
            if (!faculty.IsActive)
                throw new ConflictException(CreateTutorCommandHandler.FacultyInactiveMessage);

            var hasAssignments = await db.TutorAssignments
                .AnyAsync(a => a.TutorUserId == tutor.Id && a.IsActive, cancellationToken);
            if (hasAssignments)
                throw new ConflictException(HasAssignmentsMessage);

            tutor.AssignToFaculty(faculty.Id);
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

        await audit.WriteAsync(
            AuditAction.TutorUpdated, nameof(User), tutor.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return await TutorDetailQueries.LoadAsync(db, tutor.Id, cancellationToken);
    }
}
