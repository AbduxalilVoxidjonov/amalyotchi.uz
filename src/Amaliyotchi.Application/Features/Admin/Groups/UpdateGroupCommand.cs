using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Groups;

/// <summary><c>PUT /api/admin/groups/{id}</c>: <c>{ name, course }</c> → 200 <see cref="GroupDto"/>.
/// <see cref="Id"/> route'dan (body'da yo'q). Topilmasa → 404; nomi shu yo'nalish/o'quv yilidagi boshqa
/// guruh bilan to'qnashsa → 409.</summary>
public sealed record UpdateGroupCommand(Guid Id, string Name, int Course) : IRequest<GroupDto>;

internal sealed class UpdateGroupCommandHandler(IApplicationDbContext db, IAuditWriter audit)
    : IRequestHandler<UpdateGroupCommand, GroupDto>
{
    public async Task<GroupDto> Handle(UpdateGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await db.StudentGroups.FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Guruh topilmadi.");

        var name = request.Name.Trim();
        if (!string.Equals(group.Name, name, StringComparison.Ordinal))
        {
            var duplicate = await db.StudentGroups.AnyAsync(
                g => g.Id != request.Id && g.DirectionId == group.DirectionId && g.AcademicYearId == group.AcademicYearId
                     && g.Name.ToLower() == name.ToLower(), cancellationToken);
            if (duplicate)
                throw new ConflictException($"'{name}' guruhi bu yo'nalishda allaqachon mavjud.");
        }

        group.Update(request.Name, request.Course);

        await audit.WriteAsync(
            AuditAction.GroupUpdated, nameof(StudentGroup), group.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var academicYearName = await db.AcademicYears.AsNoTracking()
            .Where(y => y.Id == group.AcademicYearId)
            .Select(y => y.Name)
            .FirstAsync(cancellationToken);

        return new GroupDto(group.Id, group.DirectionId, group.Name, group.Course, group.IsActive, academicYearName);
    }
}
