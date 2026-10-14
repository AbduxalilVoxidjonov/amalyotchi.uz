using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Student.Tracking;

/// <summary><c>PUT /api/student/tracking/consent</c> — <c>{ "consent": true }</c> → 204: fonda kuzatuvga rozilik
/// (vaqti saqlanadi; takror <c>true</c> dastlabki vaqtni o'zgartirmaydi). <c>false</c> — rozilikni qaytarib olish
/// (keyingi ping'lar qabul qilinmaydi). <c>consent</c> yo'q → 400 <c>errors.Consent</c>.</summary>
public sealed record SetTrackingConsentCommand(bool? Consent) : IRequest;

public sealed class SetTrackingConsentCommandValidator : AbstractValidator<SetTrackingConsentCommand>
{
    public const string ConsentRequiredMessage = "Rozilik qiymati (consent: true yoki false) yuborilishi kerak.";

    public SetTrackingConsentCommandValidator()
    {
        RuleFor(x => x.Consent)
            .NotNull().WithMessage(ConsentRequiredMessage);
    }
}

internal sealed class SetTrackingConsentCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<SetTrackingConsentCommand>
{
    public async Task Handle(SetTrackingConsentCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");
        var profile = await db.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Talaba profili topilmadi.");

        if (request.Consent == true)
            profile.GiveTrackingConsent(clock.UtcNow);
        else
            profile.WithdrawTrackingConsent();

        await db.SaveChangesAsync(cancellationToken);
    }
}
