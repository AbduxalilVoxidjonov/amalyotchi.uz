using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Student.CheckIn;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Exceptions;
using MediatR;

namespace Amaliyotchi.Application.Features.Student.CheckOut;

/// <summary><c>POST /api/student/checkout</c>. 400: oyna (17:00 gacha / 18:00 dan keyin), aniqlik;
/// 409: check-in yo'q, allaqachon ketgan, radius tashqarisi. Har urinish <see cref="AttendanceEvent"/> ga yoziladi.</summary>
public sealed record CheckOutCommand(double Lat, double Lng, double Accuracy, DateTimeOffset OccurredAt)
    : IRequest<TodayDto>, IGeoRequest;

public sealed class CheckOutCommandValidator : GeoRequestValidator<CheckOutCommand>
{
    public CheckOutCommandValidator(IClock clock) : base(clock) { }
}

internal sealed class CheckOutCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<CheckOutCommand, TodayDto>
{
    public Task<TodayDto> Handle(CheckOutCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");
        var attempt = new AttendanceAttempt(db, clock, userId);

        return attempt.RunAsync(
            request,
            AttendanceEventKind.CheckOut,
            decide: s => CheckInPolicy.EvaluateCheckOut(
                new CheckOutContext(
                    s.LocalNow,
                    HasCheckedIn: s.Attendance?.HasCheckedIn ?? false,
                    AlreadyCheckedOut: s.Attendance?.HasCheckedOut ?? false,
                    AutoClosed: s.Attendance?.AutoClosed ?? false,
                    AccuracyM: request.Accuracy,
                    DistanceM: s.DistanceM,
                    RadiusM: s.Company.RadiusM),
                s.Rules),
            apply: (s, _, _) => s.Attendance!.CheckOut(s.ReceivedAt, s.DistanceM),
            cancellationToken);
    }
}
