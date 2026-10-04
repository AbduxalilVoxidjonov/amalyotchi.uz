using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Features.Faces;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Faces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Amaliyotchi.Application.Features.Tutor.Faces;

/// <summary><c>POST /api/tutor/students/{studentId}/face/approve</c> → 200 <see cref="StudentFaceDto"/>.
/// Faqat <c>pending</c> tasdiqlanadi, aks holda (yoki etalon yo'q) → 409. Ko'lamdan tashqari talaba → 404.</summary>
public sealed record ApproveFaceEnrollmentCommand(Guid StudentId) : IRequest<StudentFaceDto>;

/// <summary><c>POST /api/tutor/students/{studentId}/face/reject</c> <c>{ reason }</c> (majburiy, ≤ 500) → 200.
/// <c>pending</c> yoki <c>approved</c> rad etiladi; allaqachon rad etilgan yoki etalon yo'q → 409.</summary>
public sealed record RejectFaceEnrollmentCommand(Guid StudentId, string Reason) : IRequest<StudentFaceDto>;

/// <summary><c>POST /api/tutor/students/{studentId}/face/reset</c> → 200 (holat <c>none</c>): etalon o'chiriladi, talaba
/// qayta yuboradi (rasm fayli saqlanib qoladi). Etalon yo'q → 409.</summary>
public sealed record ResetFaceEnrollmentCommand(Guid StudentId) : IRequest<StudentFaceDto>;

public sealed class RejectFaceEnrollmentCommandValidator : AbstractValidator<RejectFaceEnrollmentCommand>
{
    public RejectFaceEnrollmentCommandValidator()
    {
        RuleFor(x => x.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Rad etish sababi majburiy.")
            .Must(r => r.Trim().Length <= StudentFaceEnrollment.RejectReasonMaxLength)
            .WithMessage($"Sabab {StudentFaceEnrollment.RejectReasonMaxLength} belgidan oshmasligi kerak.");
    }
}

internal sealed class FaceEnrollmentReviewHandler(
    IApplicationDbContext db,
    IScopeResolver scopeResolver,
    ICurrentUser currentUser,
    IClock clock,
    IAuditWriter audit,
    ITelegramMessenger messenger,
    ILogger<FaceEnrollmentReviewHandler> logger)
    : IRequestHandler<ApproveFaceEnrollmentCommand, StudentFaceDto>,
      IRequestHandler<RejectFaceEnrollmentCommand, StudentFaceDto>,
      IRequestHandler<ResetFaceEnrollmentCommand, StudentFaceDto>
{
    public const string NotSubmittedMessage = "Talaba hali yuz rasmini yubormagan.";

    public async Task<StudentFaceDto> Handle(ApproveFaceEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var (enrollment, reviewerId, chatId) = await LoadAsync(request.StudentId, cancellationToken);
        enrollment.Approve(reviewerId, clock.UtcNow);

        await audit.WriteAsync(
            AuditAction.FaceEnrollmentApproved, nameof(StudentFaceEnrollment), enrollment.Id.ToString(),
            cancellationToken: cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await NotifyAsync(chatId, "Yuz rasmingiz tyutor tomonidan tasdiqlandi. Endi check-in selfingiz shu rasm bilan solishtiriladi.",
            cancellationToken);
        return await ToDtoAsync(enrollment, cancellationToken);
    }

    public async Task<StudentFaceDto> Handle(RejectFaceEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var (enrollment, reviewerId, chatId) = await LoadAsync(request.StudentId, cancellationToken);
        enrollment.Reject(reviewerId, request.Reason, clock.UtcNow);

        await audit.WriteAsync(
            AuditAction.FaceEnrollmentRejected, nameof(StudentFaceEnrollment), enrollment.Id.ToString(),
            reason: enrollment.RejectReason, cancellationToken: cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await NotifyAsync(chatId,
            $"Yuz rasmingiz rad etildi. Sabab: {enrollment.RejectReason}\nIlovada yuzingizni qayta suratga olib yuboring.",
            cancellationToken);
        return await ToDtoAsync(enrollment, cancellationToken);
    }

    public async Task<StudentFaceDto> Handle(ResetFaceEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var (enrollment, _, _) = await LoadAsync(request.StudentId, cancellationToken);
        db.StudentFaceEnrollments.Remove(enrollment);

        await audit.WriteAsync(
            AuditAction.FaceEnrollmentReset, nameof(StudentFaceEnrollment), enrollment.Id.ToString(),
            changes: System.Text.Json.JsonSerializer.Serialize(new
            {
                studentUserId = enrollment.StudentUserId,
                status = StudentFaceDto.ToStatus(enrollment.Status).ToString().ToLowerInvariant()
            }),
            cancellationToken: cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(null, cancellationToken);
    }

    /// <summary>Ko'lam (404) → etalon (yo'q → 409) → ko'rib chiquvchi; talabaning Telegram chat'i (bildirishnoma uchun).</summary>
    private async Task<(StudentFaceEnrollment Enrollment, Guid ReviewerId, long? ChatId)> LoadAsync(
        Guid studentId, CancellationToken cancellationToken)
    {
        var reviewerId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");
        var scope = await scopeResolver.ResolveAsync(cancellationToken);

        var student = await db.StudentProfiles.AsNoTracking().InScope(scope)
            .Where(p => p.UserId == studentId)
            .Select(p => new { p.UserId, p.User.TelegramUserId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Talaba", studentId);

        var enrollment = await db.StudentFaceEnrollments
            .FirstOrDefaultAsync(f => f.StudentUserId == student.UserId, cancellationToken)
            ?? throw new ConflictException(NotSubmittedMessage);

        return (enrollment, reviewerId, student.TelegramUserId);
    }

    private async Task<StudentFaceDto> ToDtoAsync(StudentFaceEnrollment? enrollment, CancellationToken cancellationToken)
    {
        var settings = await db.LoadStudentSettingsAsync(cancellationToken);
        return StudentFaceDto.From(enrollment, settings.FaceVerificationEnabled);
    }

    /// <summary>Bot orqali shaxsiy xabar — ixtiyoriy: Telegram bog'lanmagan yoki yuborilmasa amal baribir muvaffaqiyatli
    /// (messenger istisno tashlamaydi, natija faqat log'ga).</summary>
    private async Task NotifyAsync(long? chatId, string text, CancellationToken cancellationToken)
    {
        if (chatId is not { } chat)
            return;

        var result = await messenger.SendTextAsync(chat, text, withAppButton: true, cancellationToken);
        if (result.Outcome != TelegramSendOutcome.Sent)
            logger.LogInformation("Yuz rasmi bildirishnomasi yuborilmadi: {Outcome} {Error}", result.Outcome, result.Error);
    }
}
