using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Files;
using Amaliyotchi.Domain.Leave;
using Amaliyotchi.Domain.Practice;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Student.LeaveRequests;

/// <summary><c>POST /api/student/leave-requests</c>. Sanalar davr ichida; kesishuvchi kutilayotgan/tasdiqlangan so'rov → 409.
/// <paramref name="AttachmentFileId"/> — talabaning o'zi yuklagan <c>StoredFile</c> (ruxsat hujjati); yuklash endpoint'i
/// hali yo'q, shuning uchun odatda faqat <paramref name="AttachmentName"/> keladi.</summary>
public sealed record CreateLeaveRequestCommand(
    DateOnly DateFrom,
    DateOnly DateTo,
    string Reason,
    string? AttachmentName = null,
    Guid? AttachmentFileId = null)
    : IRequest<LeaveRequestDto>;

public sealed class CreateLeaveRequestCommandValidator : AbstractValidator<CreateLeaveRequestCommand>
{
    public CreateLeaveRequestCommandValidator()
    {
        RuleFor(x => x.DateFrom)
            .NotEqual(default(DateOnly)).WithMessage("Boshlanish sanasini kiriting.");

        RuleFor(x => x.DateTo)
            .NotEqual(default(DateOnly)).WithMessage("Tugash sanasini kiriting.")
            .GreaterThanOrEqualTo(x => x.DateFrom).WithMessage("Tugash sanasi boshlanish sanasidan oldin bo'lishi mumkin emas.")
            .Must((x, to) => to.DayNumber - x.DateFrom.DayNumber + 1 <= LeaveRequest.MaxDays)
            .WithMessage($"Bitta so'rov ko'pi bilan {LeaveRequest.MaxDays} kunni qamrab oladi.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Sababni kiriting.")
            .Must(r => (r?.Trim().Length ?? 0) >= LeaveRequest.MinReasonLength)
            .WithMessage($"Sabab kamida {LeaveRequest.MinReasonLength} belgidan iborat bo'lishi kerak.")
            .MaximumLength(LeaveRequest.ReasonMaxLength).WithMessage($"Sabab {LeaveRequest.ReasonMaxLength} belgidan oshmasligi kerak.");

        RuleFor(x => x.AttachmentName)
            .MaximumLength(StoredFile.FileNameMaxLength).WithMessage("Hujjat nomi juda uzun.");
    }
}

internal sealed class CreateLeaveRequestCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<CreateLeaveRequestCommand, LeaveRequestDto>
{
    public async Task<LeaveRequestDto> Handle(CreateLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");
        var today = clock.LocalToday();

        // Ruxsat davri — so'ralgan sanalarni to'liq qamragan, yopilmagan guruh davri (davom etayotgan yoki kelgusi).
        var practice = await db.LoadStudentPracticeAsync(userId, today, PeriodPurpose.Enrollment, cancellationToken);
        var open = practice.Periods.GroupPeriods.Where(p => p.Status != PracticePeriodStatus.Closed).ToList();
        if (open.Count == 0)
            throw new DomainException("Faol amaliyot davri yo'q — ruxsat so'rab bo'lmaydi.");

        var period = open.FirstOrDefault(p => request.DateFrom >= p.StartDate && request.DateTo <= p.EndDate)
            ?? throw new DomainException("Ruxsat sanalari amaliyot davri ichida bo'lishi kerak.");

        var overlapping = await db.LeaveRequests
            .AsNoTracking()
            .AnyAsync(l => l.StudentUserId == userId
                           && l.Status != LeaveRequestStatus.Rejected
                           && l.DateFrom <= request.DateTo && l.DateTo >= request.DateFrom, cancellationToken);
        if (overlapping)
            throw new ConflictException("Bu sanalar uchun ruxsat so'rovi allaqachon bor.");

        Guid? documentFileId = null;
        string? attachmentName = request.AttachmentName;
        if (request.AttachmentFileId is { } fileId)
        {
            var file = await db.StoredFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == fileId && f.UploadedByUserId == userId, cancellationToken)
                ?? throw new NotFoundException("Hujjat fayli", fileId);
            documentFileId = file.Id;
            attachmentName ??= file.FileName;
        }

        var leave = LeaveRequest.Create(userId, period.Id, request.DateFrom, request.DateTo, request.Reason, attachmentName, documentFileId);
        db.LeaveRequests.Add(leave);
        await db.SaveChangesAsync(cancellationToken);

        return LeaveRequestMapping.ToDto(leave);
    }
}
