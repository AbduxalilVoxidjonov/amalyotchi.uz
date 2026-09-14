using Amaliyotchi.Domain.Diary;
using FluentValidation;

namespace Amaliyotchi.Application.Features.Tutor.Diaries;

public sealed class ReviewDiaryCommandValidator : AbstractValidator<ReviewDiaryCommand>
{
    public ReviewDiaryCommandValidator()
    {
        RuleFor(x => x.DiaryId).NotEmpty().WithMessage("Yozuv ko'rsatilmagan.");
        RuleFor(x => x.Action).IsInEnum().WithMessage("Amal: approve, score yoki rewrite.");

        RuleFor(x => x.Score)
            .InclusiveBetween(DiaryEntry.MinScore, DiaryEntry.MaxScore)
            .When(x => x.Score is not null)
            .WithMessage($"Ball {DiaryEntry.MinScore}–{DiaryEntry.MaxScore} oralig'ida bo'lishi kerak.");

        RuleFor(x => x.Score)
            .NotNull()
            .When(x => x.Action == DiaryReviewAction.Score)
            .WithMessage("Baholashda ball majburiy.");

        RuleFor(x => x.Comment)
            .NotEmpty()
            .When(x => x.Action == DiaryReviewAction.Rewrite)
            .WithMessage("Qayta yozish sababi (izoh) majburiy.");

        RuleFor(x => x.Comment)
            .MaximumLength(DiaryEntry.CommentMaxLength)
            .WithMessage($"Izoh {DiaryEntry.CommentMaxLength} belgidan oshmasligi kerak.");
    }
}
