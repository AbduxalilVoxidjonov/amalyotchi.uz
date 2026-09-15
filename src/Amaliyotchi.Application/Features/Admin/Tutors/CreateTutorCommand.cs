using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Hemis = Amaliyotchi.Domain.ValueObjects.HemisId;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

/// <summary><c>POST /api/admin/tutors</c>: <c>{ fullName, hemisId, phone?, password, facultyId }</c> → 201 <see cref="TutorDetail"/>.
/// Fakultet topilmasa → 404; faol bo'lmasa → 409. HEMIS ID takrorlansa (o'chirilgan hisoblar ham hisobga olinadi —
/// login identifikatori qayta ishlatilmaydi) → 409. Telefon boshqa faol hisobda bo'lsa → 409 (bazadagi unikal indeks).</summary>
public sealed record CreateTutorCommand(string FullName, string HemisId, string? Phone, string Password, Guid FacultyId)
    : IRequest<TutorDetail>;

internal sealed class CreateTutorCommandHandler(IApplicationDbContext db, IPasswordHasher passwordHasher, IAuditWriter audit)
    : IRequestHandler<CreateTutorCommand, TutorDetail>
{
    public const string HemisIdTakenMessage = "Bu HEMIS ID bilan foydalanuvchi mavjud.";
    public const string PhoneTakenMessage = "Bu telefon raqami bilan foydalanuvchi mavjud.";
    public const string FacultyInactiveMessage = "Fakultet faol emas.";

    public async Task<TutorDetail> Handle(CreateTutorCommand request, CancellationToken cancellationToken)
    {
        var faculty = await db.Faculties.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.FacultyId, cancellationToken)
            ?? throw new NotFoundException("Fakultet topilmadi.");
        if (!faculty.IsActive)
            throw new ConflictException(FacultyInactiveMessage);

        var hemisId = Hemis.Normalize(request.HemisId);
        // O'chirilgan hisoblar ham hisobga olinadi — global soft-delete filtri chetlab o'tiladi.
        var hemisTaken = await db.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.HemisId == hemisId, cancellationToken);
        if (hemisTaken)
            throw new ConflictException(HemisIdTakenMessage);

        var phone = TutorPhone.NormalizeOrNull(request.Phone);
        if (phone is not null)
        {
            var phoneTaken = await db.Users.AnyAsync(u => u.PhoneNumber == phone, cancellationToken);
            if (phoneTaken)
                throw new ConflictException(PhoneTakenMessage);
        }

        var tutor = User.CreateWithPassword(
            request.FullName, hemisId, phone, passwordHasher.Hash(request.Password), UserRole.Tutor, faculty.Id);
        db.Users.Add(tutor);

        await audit.WriteAsync(
            AuditAction.TutorCreated, nameof(User), tutor.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return await TutorDetailQueries.LoadAsync(db, tutor.Id, cancellationToken);
    }
}

/// <summary>Ixtiyoriy telefon: bo'sh → null, aks holda E.164 (<c>+998…</c>) — takrorlanish tekshiruvi
/// bazadagi normallashgan qiymat bilan solishtirilishi uchun.</summary>
internal static class TutorPhone
{
    public static string? NormalizeOrNull(string? phone)
        => string.IsNullOrWhiteSpace(phone) ? null : Domain.ValueObjects.PhoneNumber.Normalize(phone);
}
