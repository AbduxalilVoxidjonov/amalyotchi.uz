using System.Text.Json;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary><c>PUT /api/admin/students/{id}/hemis-id</c>: <c>{ hemisId }</c> → 204 (v3.34). Talabaning ilova logini
/// (<c>StudentProfile.HemisId</c>) — xato kiritilgan yoki yangilangan HEMIS ID ni tuzatish. Qoidalar yaratish/import bilan
/// bir xil (5–20 raqam, 400 <c>errors.hemisId</c>); boshqa talaba yoki admin logini band → 409; joriy bilan bir xil → 204
/// (o'zgarishsiz). Parol va sessiyalarga tegmaydi. Talaba topilmasa → 404. Audit: <c>studentHemisIdChanged</c>.</summary>
public sealed record SetStudentHemisIdCommand(Guid Id, string? HemisId) : IRequest;

public sealed class SetStudentHemisIdCommandValidator : AbstractValidator<SetStudentHemisIdCommand>
{
    public SetStudentHemisIdCommandValidator()
    {
        RuleFor(x => x.HemisId).Custom((value, context) =>
        {
            if (StudentFieldRules.HemisIdError(value, out _) is { } error)
                context.AddFailure(CreateStudentFields.HemisId, error);
        });
    }
}

internal sealed class SetStudentHemisIdCommandHandler(IApplicationDbContext db, IScopeResolver scopeResolver, IAuditWriter audit)
    : IRequestHandler<SetStudentHemisIdCommand>
{
    public async Task Handle(SetStudentHemisIdCommand request, CancellationToken cancellationToken)
    {
        StudentFieldRules.HemisIdError(request.HemisId, out var hemisId);
        var scope = await scopeResolver.ResolveAsync(cancellationToken);

        var profile = await db.StudentProfiles.InScope(scope)
            .FirstOrDefaultAsync(p => p.UserId == request.Id, cancellationToken)
            ?? throw new NotFoundException("Talaba", request.Id);

        var old = profile.HemisId;
        if (old == hemisId)
            return;

        if (await StudentRegistration.IsHemisTakenAsync(db, hemisId, cancellationToken))
            throw new ConflictException(StudentCreateMessages.HemisTakenMessage);

        profile.ChangeHemisId(hemisId);
        await audit.WriteAsync(
            AuditAction.StudentHemisIdChanged, nameof(User), request.Id.ToString(),
            changes: JsonSerializer.Serialize(new { old, @new = hemisId }),
            cancellationToken: cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
