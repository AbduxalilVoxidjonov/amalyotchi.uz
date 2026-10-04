using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Admins;

/// <summary><c>PUT /api/admin/admins/{id}</c> (bosh admin): <c>{ fullName, phoneNumber? }</c> → 200 <see cref="AdminAccountDto"/>.
/// <see cref="Id"/> route'dan. Admin topilmasa → 404; telefon boshqa hisobda → 409. Login bu yerda o'zgarmaydi.</summary>
public sealed record UpdateAdminCommand(Guid Id, string FullName, string? PhoneNumber) : IRequest<AdminAccountDto>;

public sealed class UpdateAdminCommandValidator : AbstractValidator<UpdateAdminCommand>
{
    public UpdateAdminCommandValidator()
    {
        RuleFor(x => x.FullName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(AccountValidationRules.FullNameRequiredMessage)
            .Must(AccountValidationRules.IsValidFullName).WithMessage(AccountValidationRules.FullNameLengthMessage);

        RuleFor(x => x.PhoneNumber)
            .Must(AccountValidationRules.IsValidOptionalPhone).WithMessage(AccountValidationRules.PhoneFormatMessage);
    }
}

internal sealed class UpdateAdminCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, IAuditWriter audit)
    : IRequestHandler<UpdateAdminCommand, AdminAccountDto>
{
    public async Task<AdminAccountDto> Handle(UpdateAdminCommand request, CancellationToken cancellationToken)
    {
        await AdminAccounts.EnsureSuperAdminAsync(db, currentUser, cancellationToken);
        var admin = await AdminAccounts.LoadAdminAsync(db, request.Id, withActiveTokens: false, cancellationToken);

        var phone = AccountValidationRules.NormalizePhoneOrNull(request.PhoneNumber);
        if (phone is not null && !string.Equals(phone, admin.PhoneNumber, StringComparison.Ordinal)
            && await db.Users.AnyAsync(u => u.Id != admin.Id && u.PhoneNumber == phone, cancellationToken))
            throw new ConflictException(AdminAccounts.PhoneTakenMessage);

        admin.Rename(request.FullName);
        admin.ChangePhoneNumber(phone);

        await audit.WriteAsync(AuditAction.AdminUpdated, nameof(User), admin.Id.ToString(), cancellationToken: cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return AdminAccounts.ToDto(admin);
    }
}
