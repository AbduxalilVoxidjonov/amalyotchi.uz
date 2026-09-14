using Amaliyotchi.Domain.Leave;
using FluentValidation;

namespace Amaliyotchi.Application.Features.Tutor.LeaveRequests;

public sealed class DecideLeaveRequestCommandValidator : AbstractValidator<DecideLeaveRequestCommand>
{
    public DecideLeaveRequestCommandValidator()
    {
        RuleFor(x => x.LeaveRequestId).NotEmpty().WithMessage("So'rov ko'rsatilmagan.");
        RuleFor(x => x.Decision).IsInEnum().WithMessage("Qaror: approve yoki reject.");
        RuleFor(x => x.Comment)
            .MaximumLength(LeaveRequest.CommentMaxLength)
            .WithMessage($"Izoh {LeaveRequest.CommentMaxLength} belgidan oshmasligi kerak.");
    }
}
