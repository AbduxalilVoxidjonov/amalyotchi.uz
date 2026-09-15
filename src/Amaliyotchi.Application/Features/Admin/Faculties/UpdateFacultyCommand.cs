using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Faculties;

/// <summary><c>PUT /api/admin/faculties/{id}</c>: <c>{ name, code }</c> → 200 <see cref="FacultyDto"/>.
/// <see cref="Id"/> route'dan keladi (body'da yo'q). Topilmasa → 404; boshqa fakultetning kodi bilan
/// to'qnashsa → 409.</summary>
public sealed record UpdateFacultyCommand(Guid Id, string Name, string Code) : IRequest<FacultyDto>;

internal sealed class UpdateFacultyCommandHandler(IApplicationDbContext db, IAuditWriter audit)
    : IRequestHandler<UpdateFacultyCommand, FacultyDto>
{
    public async Task<FacultyDto> Handle(UpdateFacultyCommand request, CancellationToken cancellationToken)
    {
        var faculty = await db.Faculties.FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Fakultet topilmadi.");

        var code = request.Code.Trim().ToUpperInvariant();
        if (!string.Equals(faculty.Code, code, StringComparison.Ordinal))
        {
            var duplicate = await db.Faculties.AnyAsync(
                f => f.Id != request.Id && f.Code == code, cancellationToken);
            if (duplicate)
                throw new ConflictException($"'{code}' kodli fakultet allaqachon mavjud.");
        }

        faculty.Rename(request.Name);
        faculty.ChangeCode(request.Code);

        await audit.WriteAsync(
            AuditAction.FacultyUpdated, nameof(Faculty), faculty.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return new FacultyDto(faculty.Id, faculty.Name, faculty.Code, faculty.IsActive);
    }
}
