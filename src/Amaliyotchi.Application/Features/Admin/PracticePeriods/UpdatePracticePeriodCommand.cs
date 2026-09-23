using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using FluentValidation;
using MediatR;

namespace Amaliyotchi.Application.Features.Admin.PracticePeriods;

/// <summary><c>PUT /api/admin/practice-periods/{id}</c>: <c>{ name, startDate, endDate }</c> → 200
/// <see cref="PracticePeriodDetail"/>. <see cref="Id"/> route'dan. Topilmasa → 404; yopilgan → 409; faol davrda
/// <c>startDate</c> o'zgarsa yoki yangi <c>endDate</c> bugundan oldin bo'lsa → 400. Sanalar o'zgarsa — ustma-ust
/// tushish qayta tekshiriladi (409) va <c>requiredDays</c> qayta hisoblanadi.</summary>
public sealed record UpdatePracticePeriodCommand(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate)
    : IRequest<PracticePeriodDetail>;

public sealed class UpdatePracticePeriodCommandValidator : AbstractValidator<UpdatePracticePeriodCommand>
{
    public UpdatePracticePeriodCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Davr ko'rsatilmagan.");
        PracticePeriodValidationRules.ApplyNameAndDates(this, x => x.Name, x => x.StartDate, x => x.EndDate);
    }
}

internal sealed class UpdatePracticePeriodCommandHandler(IApplicationDbContext db, IAuditWriter audit, IClock clock)
    : IRequestHandler<UpdatePracticePeriodCommand, PracticePeriodDetail>
{
    public async Task<PracticePeriodDetail> Handle(UpdatePracticePeriodCommand request, CancellationToken cancellationToken)
    {
        var period = await PracticePeriodQueries.FindTrackedAsync(db, request.Id, cancellationToken);
        if (!period.IsOpen)
            throw new ConflictException("Yopilgan davrni tahrirlab bo'lmaydi.");

        var datesChanged = period.StartDate != request.StartDate || period.EndDate != request.EndDate;
        if (datesChanged)
        {
            var today = clock.LocalToday();
            var requiredDays = await PracticePeriodQueries.CountRequiredDaysAsync(
                db, request.StartDate, request.EndDate, period.WorkDays, cancellationToken);

            // Domain qoidalari (faol davr boshlanishi, bugundan oldingi tugash) ustma-ust tekshiruvidan oldin — 400 ustun.
            period.Reschedule(request.StartDate, request.EndDate, requiredDays, today);

            await PracticePeriodQueries.EnsureNoOverlapAsync(
                db, period.Groups.Select(g => g.StudentGroupId).ToList(),
                request.StartDate, request.EndDate, period.Id, cancellationToken);
        }

        period.Rename(request.Name);

        await audit.WriteAsync(
            AuditAction.PracticePeriodUpdated, nameof(PracticePeriod), period.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return await PracticePeriodQueries.LoadDetailAsync(db, clock, period.Id, cancellationToken);
    }
}
