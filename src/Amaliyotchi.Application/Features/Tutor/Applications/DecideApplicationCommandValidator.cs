using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Practice;
using FluentValidation;

namespace Amaliyotchi.Application.Features.Tutor.Applications;

public sealed class DecideApplicationCommandValidator : AbstractValidator<DecideApplicationCommand>
{
    /// <summary>Kontrakt: radius qadami 50 m.</summary>
    public const int RadiusStepM = 50;

    public DecideApplicationCommandValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty().WithMessage("Ariza ko'rsatilmagan.");
        RuleFor(x => x.Decision).IsInEnum().WithMessage("Qaror: approve, return yoki reject.");

        RuleFor(x => x.Comment)
            .MaximumLength(PracticeApplication.CommentMaxLength)
            .WithMessage($"Izoh {PracticeApplication.CommentMaxLength} belgidan oshmasligi kerak.");

        When(x => x.Decision == ApplicationDecision.Approve, () =>
        {
            RuleFor(x => x.RadiusM)
                .NotNull().WithMessage("Tasdiqlashda radius majburiy.")
                .InclusiveBetween(Company.MinRadiusM, Company.MaxRadiusM)
                .WithMessage($"Radius {Company.MinRadiusM}–{Company.MaxRadiusM} m oralig'ida bo'lishi kerak.")
                .Must(r => r is null || r % RadiusStepM == 0)
                .WithMessage($"Radius {RadiusStepM} m qadam bilan bo'lishi kerak.");

            RuleForEach(x => x.Checklist)
                .InclusiveBetween(0, PracticeApplication.ChecklistItemCount - 1)
                .WithMessage($"Tekshiruv ro'yxati indekslari 0–{PracticeApplication.ChecklistItemCount - 1} oralig'ida bo'lishi kerak.");
        });

        When(x => x.Decision is ApplicationDecision.Return or ApplicationDecision.Reject, () =>
        {
            RuleFor(x => x.Comment)
                .NotEmpty().WithMessage("Qaytarish/rad etish sababi (izoh) majburiy.");
        });
    }
}
