using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Student.CheckIn;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Exceptions;
using MediatR;

namespace Amaliyotchi.Application.Features.Student.CheckOut;

/// <summary><c>POST /api/student/checkout</c> (multipart yoki JSON). 400: oyna (17:00 gacha / 18:00 dan keyin),
/// aniqlik, rasm qoidalari; 409: check-in yo'q, allaqachon ketgan, radius tashqarisi.
/// Har urinish <see cref="AttendanceEvent"/> ga rasmi bilan yoziladi.</summary>
public sealed record CheckOutCommand(double Lat, double Lng, double Accuracy, DateTimeOffset OccurredAt, UploadedFile? Photo = null)
    : IRequest<TodayDto>, IGeoRequest;

public sealed class CheckOutCommandValidator : GeoRequestValidator<CheckOutCommand>
{
    public CheckOutCommandValidator(IClock clock) : base(clock) { }
}

internal sealed class CheckOutCommandHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IFileStorage storage)
    : IRequestHandler<CheckOutCommand, TodayDto>
{
    public Task<TodayDto> Handle(CheckOutCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");
        var attempt = new AttendanceAttempt(db, clock, storage, userId);

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
            apply: (s, _, attempt) => s.Attendance!.CheckOut(s.ReceivedAt, s.DistanceM, attempt.PhotoFileId),
            cancellationToken);
    }
}
