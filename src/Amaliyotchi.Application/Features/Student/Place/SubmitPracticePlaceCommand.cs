using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Practice;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Companies;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Tin = Amaliyotchi.Domain.ValueObjects.Tin;

namespace Amaliyotchi.Application.Features.Student.Place;

/// <summary><c>POST /api/student/place</c>: <c>{ tin }</c> → 201 <see cref="PracticePlaceDto"/>.
/// Talaba korxona ma'lumotini QO'LDA kiritmaydi — faqat STIR yozadi, qolgani admin oldindan
/// kiritgan yozuvdan olinadi (nom, manzil, koordinata, radius, rahbar). Ariza <c>Submitted</c>
/// holatida tyutorga boradi; radius korxonanikidan olinadi.
/// Ariza davri — davom etayotgan, bo'lmasa eng yaqin kelgusi davr (<see cref="PeriodPurpose.Enrollment"/>).
/// STIR bilan faol korxona topilmasa → 404; ochiq (davom etayotgan/kelgusi) davr yo'q → 409; ariza allaqachon bor → 409
/// (qayta ishlashga qaytarilgan bo'lsa — qayta yuboriladi).</summary>
public sealed record SubmitPracticePlaceCommand(string Tin) : IRequest<PracticePlaceDto>;

/// <summary>Talabaga ko'rinadigan xabarlar.</summary>
public static class SubmitPlaceMessages
{
    public const string NoPeriodMessage =
        "Sizga faol amaliyot davri biriktirilmagan — tyutoringizga murojaat qiling.";
    public const string PendingMessage =
        "Arizangiz ko'rib chiqilmoqda — tyutor qaroridan keyin o'zgartirish mumkin.";
    public const string AlreadyApprovedMessage =
        "Sizga allaqachon amaliyot joyi biriktirilgan. O'zgartirish uchun tyutoringizga murojaat qiling.";
}

internal sealed class SubmitPracticePlaceCommandHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, ISender sender)
    : IRequestHandler<SubmitPracticePlaceCommand, PracticePlaceDto>
{
    public async Task<PracticePlaceDto> Handle(SubmitPracticePlaceCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");
        var today = clock.LocalToday();
        var now = clock.UtcNow;

        // Faqat FAOL korxona (faolsizlantirilgani STIR qidiruvida ham chiqmaydi).
        var company = await sender.Send(new GetCompanyByTinQuery(Tin.Normalize(request.Tin)), cancellationToken);

        var groupId = await db.StudentProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => (Guid?)p.StudentGroupId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Talaba profili topilmadi.");

        // Ariza davri: davom etayotgan → eng yaqin kelgusi (tanaffusda talaba bahorgi davrga oldindan ariza beradi).
        // Tugagan/yopilgan davrga ariza berilmaydi.
        var periods = await db.LoadStudentPeriodsAsync(userId, groupId, cancellationToken);
        var period = periods.Default(today, PeriodPurpose.Enrollment)
            ?? throw new ConflictException(SubmitPlaceMessages.NoPeriodMessage);

        var applications = await db.PracticeApplications
            .Where(a => a.StudentUserId == userId && a.PeriodId == period.Id)
            .OrderByDescending(a => a.SubmittedAt)
            .ToListAsync(cancellationToken);

        var current = applications.FirstOrDefault(a => a.Status is ApplicationStatus.Approved or ApplicationStatus.Completed)
                      ?? applications.FirstOrDefault();

        switch (current?.Status)
        {
            case ApplicationStatus.Approved or ApplicationStatus.Completed:
                throw new ConflictException(SubmitPlaceMessages.AlreadyApprovedMessage);

            case ApplicationStatus.Submitted:
                throw new ConflictException(SubmitPlaceMessages.PendingMessage);

            case ApplicationStatus.RevisionNeeded:
                current.Resubmit(company.Id, company.RadiusM, current.ContractFileId, now);
                break;

            default:
                db.PracticeApplications.Add(PracticeApplication.Create(
                    userId, period.Id, company.Id, company.RadiusM, contractFileId: null, submittedAt: now));
                break;
        }

        await db.SaveChangesAsync(cancellationToken);

        return await sender.Send(new GetPracticePlaceQuery(), cancellationToken);
    }
}

public sealed class SubmitPracticePlaceCommandValidator : AbstractValidator<SubmitPracticePlaceCommand>
{
    public const string TinRequiredMessage = "STIR ni kiriting.";
    public const string TinFormatMessage = "STIR 9 ta raqamdan iborat bo'lishi kerak. Namuna: 123456789";

    public SubmitPracticePlaceCommandValidator()
        => RuleFor(x => x.Tin)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(TinRequiredMessage)
            .Must(tin => Tin.TryNormalize(tin, out _)).WithMessage(TinFormatMessage);
}
