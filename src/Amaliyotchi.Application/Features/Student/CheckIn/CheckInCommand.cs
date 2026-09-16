using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using MediatR;

namespace Amaliyotchi.Application.Features.Student.CheckIn;

/// <summary><c>POST /api/student/checkin</c> (multipart yoki JSON). Javob — yangilangan <see cref="TodayDto"/>.
/// 400: oyna ochilmagan/yopiq, ish kuni emas, GPS aniqligi yomon, davr, rasm qoidalari; 409: radius tashqarisi,
/// allaqachon belgilangan. Har urinish (rad etilgani ham) <see cref="AttendanceEvent"/> ga rasmi bilan yoziladi.</summary>
public sealed record CheckInCommand(double Lat, double Lng, double Accuracy, DateTimeOffset OccurredAt, UploadedFile? Photo = null)
    : IRequest<TodayDto>, IGeoRequest;

public sealed class CheckInCommandValidator : GeoRequestValidator<CheckInCommand>
{
    public CheckInCommandValidator(IClock clock) : base(clock) { }
}

internal sealed class CheckInCommandHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IFileStorage storage)
    : IRequestHandler<CheckInCommand, TodayDto>
{
    public Task<TodayDto> Handle(CheckInCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");
        var attempt = new AttendanceAttempt(db, clock, storage, userId);

        return attempt.RunAsync(
            request,
            AttendanceEventKind.CheckIn,
            decide: s => CheckInPolicy.Evaluate(
                new CheckInContext(
                    s.Today,
                    s.LocalNow,
                    ApplicationApproved: s.Practice.IsApproved,
                    PeriodStarted: s.Today >= s.Period.StartDate,
                    PeriodEnded: s.Today > s.Period.EndDate || s.Period.Status == PracticePeriodStatus.Closed,
                    s.Period.WorkDays,
                    IsHoliday: s.Practice.IsHoliday(s.Today),
                    HasApprovedLeave: s.HasApprovedLeave,
                    AlreadyCheckedIn: s.Attendance is not null,
                    AccuracyM: request.Accuracy,
                    DistanceM: s.DistanceM,
                    RadiusM: s.Company.RadiusM),
                s.Rules),
            apply: (s, verdict, attempt) =>
            {
                var attendance = DailyAttendance.CheckIn(
                    userId, s.Period.Id, s.Today, s.ReceivedAt, s.DistanceM, request.Accuracy, verdict, attempt.PhotoFileId);

                var suspicion = SuspiciousDetector.Inspect(s.Location, s.ReceivedAt, s.Today, s.RecentEvents);
                if (suspicion is not null)
                    attendance.MarkSuspicious(suspicion);

                db.DailyAttendances.Add(attendance);
            },
            cancellationToken);
    }
}
