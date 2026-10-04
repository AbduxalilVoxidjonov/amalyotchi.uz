using System.Text.Json;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Application.Features.Auth.ChangeLogin;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Admins;

/// <summary><c>POST /api/admin/admins</c> (bosh admin): <c>{ fullName, login, phoneNumber?, password? }</c> → 201
/// <see cref="CreatedAdminDto"/>. <c>password</c> berilsa — o'sha parol o'rnatiladi (tyutor yaratishdagi kabi,
/// <c>mustChangePassword=false</c>); berilmasa — server vaqtinchalik parol yaratadi, javobda <c>temporaryPassword</c>
/// sifatida BIR MARTA qaytadi va admin birinchi kirishda parolni almashtiradi (<c>mustChangePassword=true</c>).
/// Login band (o'chirilgan hisoblar va talaba HEMIS ID'lari ham) yoki telefon band → 409. Yangi admin bosh admin emas.</summary>
public sealed record CreateAdminCommand(string FullName, string Login, string? PhoneNumber, string? Password)
    : IRequest<CreatedAdminDto>;

public sealed class CreateAdminCommandValidator : AbstractValidator<CreateAdminCommand>
{
    public CreateAdminCommandValidator()
    {
        RuleFor(x => x.FullName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(AccountValidationRules.FullNameRequiredMessage)
            .Must(AccountValidationRules.IsValidFullName).WithMessage(AccountValidationRules.FullNameLengthMessage);

        RuleFor(x => x.Login)
            .Custom((login, context) =>
            {
                if (LoginAvailability.FormatError(login, out _) is { } error)
                    context.AddFailure(error);
            });

        RuleFor(x => x.PhoneNumber)
            .Must(AccountValidationRules.IsValidOptionalPhone).WithMessage(AccountValidationRules.PhoneFormatMessage);

        RuleFor(x => x.Password)
            .Must(p => string.IsNullOrEmpty(p) || AccountValidationRules.IsValidPassword(p))
            .WithMessage(AccountValidationRules.PasswordLengthMessage);
    }
}

internal sealed class CreateAdminCommandHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IPasswordHasher passwordHasher, IAuditWriter audit)
    : IRequestHandler<CreateAdminCommand, CreatedAdminDto>
{
    public async Task<CreatedAdminDto> Handle(CreateAdminCommand request, CancellationToken cancellationToken)
    {
        await AdminAccounts.EnsureSuperAdminAsync(db, currentUser, cancellationToken);

        LoginAvailability.FormatError(request.Login, out var login);
        if (await LoginAvailability.IsTakenAsync(db, login, exceptUserId: null, cancellationToken))
            throw new ConflictException(LoginAvailability.TakenMessage);

        var phone = AccountValidationRules.NormalizePhoneOrNull(request.PhoneNumber);
        if (phone is not null && await db.Users.AnyAsync(u => u.PhoneNumber == phone, cancellationToken))
            throw new ConflictException(AdminAccounts.PhoneTakenMessage);

        var generated = string.IsNullOrEmpty(request.Password) ? TemporaryPasswords.Generate() : null;
        var hash = passwordHasher.Hash(generated ?? request.Password!);
        var admin = User.CreateWithPassword(request.FullName, login, phone, hash, UserRole.Admin);
        if (generated is not null)
            admin.SetTemporaryPassword(hash);
        db.Users.Add(admin);

        await audit.WriteAsync(
            AuditAction.AdminCreated, nameof(User), admin.Id.ToString(),
            changes: JsonSerializer.Serialize(new { login, generatedPassword = generated is not null }),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var dto = AdminAccounts.ToDto(admin);
        return new CreatedAdminDto(
            dto.Id, dto.FullName, dto.Login, dto.PhoneNumber, dto.IsActive, dto.IsSuperAdmin, dto.LastLoginAt, dto.CreatedAt,
            generated);
    }
}
