using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Faculties;

/// <summary><c>POST /api/admin/faculties</c>: <c>{ name, code }</c> → 201 <see cref="FacultyDto"/>.
/// Kod (faol fakultetlar orasida, case-insensitive) takrorlansa → 409.</summary>
public sealed record CreateFacultyCommand(string Name, string Code) : IRequest<FacultyDto>;

internal sealed class CreateFacultyCommandHandler(IApplicationDbContext db, IAuditWriter audit)
    : IRequestHandler<CreateFacultyCommand, FacultyDto>
{
    public async Task<FacultyDto> Handle(CreateFacultyCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();

        // Bazadagi unique indeks (is_deleted = false filtri bilan) himoya qiladi,
        // lekin foydalanuvchiga aniq 409 xabari uchun oldindan tekshiramiz.
        var exists = await db.Faculties.AnyAsync(f => f.Code == code, cancellationToken);
        if (exists)
            throw new ConflictException($"'{code}' kodli fakultet allaqachon mavjud.");

        var faculty = Faculty.Create(request.Name, request.Code);
        db.Faculties.Add(faculty);

        await audit.WriteAsync(
            AuditAction.FacultyCreated, nameof(Faculty), faculty.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return new FacultyDto(faculty.Id, faculty.Name, faculty.Code, faculty.IsActive);
    }
}
