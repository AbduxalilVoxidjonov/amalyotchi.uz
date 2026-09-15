using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Faculties;

/// <summary><c>DELETE /api/admin/faculties/{id}</c> → 204. Soft delete: faqat fakultetning o'zi
/// arxivlanadi (kaskad emas). Topilmasa (yoki allaqachon o'chirilgan) → 404. O'chirilmagan kafedrasi
/// bo'lsa → 409; kafedrasi bo'lmasa-yu biriktirilgan (o'chirilmagan) foydalanuvchi bo'lsa ham → 409 —
/// avval ularni boshqa fakultetga ko'chirish kerak.</summary>
public sealed record DeleteFacultyCommand(Guid Id) : IRequest;

internal sealed class DeleteFacultyCommandHandler(IApplicationDbContext db, IAuditWriter audit, IClock clock)
    : IRequestHandler<DeleteFacultyCommand>
{
    public async Task Handle(DeleteFacultyCommand request, CancellationToken cancellationToken)
    {
        var faculty = await db.Faculties.FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Fakultet topilmadi.");

        var hasDepartments = await db.Departments.AnyAsync(d => d.FacultyId == request.Id, cancellationToken);
        if (hasDepartments)
            throw new ConflictException("Fakultetda kafedralar bor — avval ularni o'chiring.");

        // Talaba/tyutorning asosiy fakulteti yoki tyutorning qo'shimcha fakulteti (tutor_faculties) bo'lsa — 409.
        var hasUsers = await db.Users.AnyAsync(u => u.FacultyId == request.Id, cancellationToken)
                       || await db.TutorFaculties.AnyAsync(tf => tf.FacultyId == request.Id
                                                                 && db.Users.Any(u => u.Id == tf.TutorUserId), cancellationToken);
        if (hasUsers)
            throw new ConflictException(
                "Fakultetga guruhlar, tyutorlar yoki talabalar biriktirilgan — avval ularni boshqa fakultetga ko'chiring.");

        faculty.Delete(clock.UtcNow);

        await audit.WriteAsync(
            AuditAction.FacultyDeleted, nameof(Faculty), faculty.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}
