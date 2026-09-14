using Amaliyotchi.Domain.Grading;
using FluentValidation;

namespace Amaliyotchi.Application.Features.Tutor.Grading;

public sealed class UpdateGradingCommandValidator : AbstractValidator<UpdateGradingCommand>
{
    public UpdateGradingCommandValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty().WithMessage("Talaba ko'rsatilmagan.");

        RuleFor(x => x.TutorPoints)
            .InclusiveBetween(0, PracticeGrade.MaxTutorPoints)
            .When(x => x.TutorPoints is not null)
            .WithMessage($"Tyutor bali 0–{PracticeGrade.MaxTutorPoints} oralig'ida bo'lishi kerak.");

        RuleFor(x => x.ReferencePoints)
            .InclusiveBetween(0, PracticeGrade.MaxReferencePoints)
            .When(x => x.ReferencePoints is not null)
            .WithMessage($"Tavsifnoma bali 0–{PracticeGrade.MaxReferencePoints} oralig'ida bo'lishi kerak.");
    }
}
