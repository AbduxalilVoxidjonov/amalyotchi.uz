using System.Text.Json;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Hemis = Amaliyotchi.Domain.ValueObjects.HemisId;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

/// <summary><c>POST /api/admin/tutors</c>: <c>{ fullName, hemisId, phone?, password, facultyIds[] }</c> → 201 <see cref="TutorDetail"/>.
/// Fakultetlardan biri topilmasa → 404; faol bo'lmasa → 409 ("Fakultet faol emas: &lt;nom&gt;"). HEMIS ID takrorlansa
/// (o'chirilgan hisoblar ham hisobga olinadi — login identifikatori qayta ishlatilmaydi) → 409. Telefon boshqa faol hisobda
/// bo'lsa → 409 (bazadagi unikal indeks). Ro'yxatning birinchisi — asosiy fakultet (<c>User.FacultyId</c>).</summary>
public sealed record CreateTutorCommand(string FullName, string HemisId, string? Phone, string Password, IReadOnlyList<Guid> FacultyIds)
    : IRequest<TutorDetail>;

internal sealed class CreateTutorCommandHandler(IApplicationDbContext db, IPasswordHasher passwordHasher, IAuditWriter audit)
    : IRequestHandler<CreateTutorCommand, TutorDetail>
{
    public const string HemisIdTakenMessage = "Bu HEMIS ID bilan foydalanuvchi mavjud.";
    public const string PhoneTakenMessage = "Bu telefon raqami bilan foydalanuvchi mavjud.";

    public async Task<TutorDetail> Handle(CreateTutorCommand request, CancellationToken cancellationToken)
    {
        var facultyIds = await TutorFaculties.EnsureActiveAsync(db, request.FacultyIds, cancellationToken);

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
            request.FullName, hemisId, phone, passwordHasher.Hash(request.Password), UserRole.Tutor, facultyIds);
        db.Users.Add(tutor);

        await audit.WriteAsync(
            AuditAction.TutorCreated, nameof(User), tutor.Id.ToString(),
            changes: JsonSerializer.Serialize(new { facultyIds }),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return await TutorDetailQueries.LoadAsync(db, tutor.Id, cancellationToken);
    }
}

/// <summary>Create/Update uchun fakultetlar to'plamini tekshirish.</summary>
internal static class TutorFaculties
{
    public const string NotFoundMessage = "Fakultet topilmadi.";

    /// <summary>Takrorlar olib tashlanadi (tartib saqlanadi). Bittasi topilmasa (yoki o'chirilgan) → 404; faol bo'lmasa →
    /// 409 "Fakultet faol emas: &lt;nom&gt;" (birinchi uchragani). Qaytgan ro'yxat — takrorsiz id'lar, so'rov tartibida.</summary>
    public static async Task<IReadOnlyList<Guid>> EnsureActiveAsync(
        IApplicationDbContext db, IReadOnlyCollection<Guid> facultyIds, CancellationToken cancellationToken)
    {
        var ids = facultyIds.Distinct().ToList();
        var faculties = await db.Faculties.AsNoTracking()
            .Where(f => ids.Contains(f.Id))
            .Select(f => new { f.Id, f.Name, f.IsActive })
            .ToListAsync(cancellationToken);

        foreach (var id in ids)
        {
            var faculty = faculties.FirstOrDefault(f => f.Id == id)
                ?? throw new NotFoundException(NotFoundMessage);
            if (!faculty.IsActive)
                throw new ConflictException($"Fakultet faol emas: {faculty.Name}");
        }

        return ids;
    }
}

/// <summary>Ixtiyoriy telefon: bo'sh → null, aks holda E.164 (<c>+998…</c>) — takrorlanish tekshiruvi
/// bazadagi normallashgan qiymat bilan solishtirilishi uchun.</summary>
internal static class TutorPhone
{
    public static string? NormalizeOrNull(string? phone)
        => string.IsNullOrWhiteSpace(phone) ? null : Domain.ValueObjects.PhoneNumber.Normalize(phone);
}
