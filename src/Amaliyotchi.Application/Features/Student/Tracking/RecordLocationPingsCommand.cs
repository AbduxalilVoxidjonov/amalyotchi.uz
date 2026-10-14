using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Student.Tracking;

/// <summary><c>POST /api/student/location-pings</c> — fonda kuzatuv paketi (1..100 ping, eskidan yangiga).
/// Qoidalar (docs/LIVE-JOYLASHUV.md):
/// <list type="bullet">
/// <item>sozlama <c>trackingEnabled=false</c> yoki rozilik yo'q → 200, <c>accepted: 0</c> (klient <c>tracking</c> bo'yicha to'xtaydi);</item>
/// <item><c>occurredAt</c> 24 soatdan eski yoki 2 daqiqadan ko'proq kelajakda → shu ping tashlanadi;</item>
/// <item>bir xil <c>occurredAt</c> (talaba bo'yicha, millisekund aniqlikda) — idempotent, takrori tashlanadi;</item>
/// <item>ping o'z kunining sessiyasiga (<c>checkInAt ≤ occurredAt ≤ checkOutAt</c> yoki avtomatik yopilish vaqti) tushmasa —
/// tashlanadi (xato emas). Offline navbat check-out'dan keyin yuborilsa ham sessiya ichidagi ping'lar qabul qilinadi;</item>
/// <item>koordinatasi/aniqligi yaroqsiz ping ham tashlanadi (butun paket rad etilmaydi — navbat tiqilib qolmasin).</item>
/// </list>
/// Holat (<c>inside|outside|inaccurate</c>) check-in geozona qoidasi bilan server'da hisoblanadi.</summary>
public sealed record RecordLocationPingsCommand(IReadOnlyList<LocationPingInput?>? Pings) : IRequest<LocationPingsResultDto>;

public sealed class RecordLocationPingsCommandValidator : AbstractValidator<RecordLocationPingsCommand>
{
    public RecordLocationPingsCommandValidator()
    {
        RuleFor(x => x.Pings)
            .NotNull().WithMessage("Ping'lar ro'yxati (pings) yuborilishi kerak.")
            .Must(p => p is null || p.Count is >= 1 and <= LocationTracking.MaxPingsPerBatch)
            .WithMessage($"Bitta so'rovda 1 dan {LocationTracking.MaxPingsPerBatch} tagacha ping yuborilishi kerak.");
    }
}

internal sealed class RecordLocationPingsCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<RecordLocationPingsCommand, LocationPingsResultDto>
{
    public async Task<LocationPingsResultDto> Handle(RecordLocationPingsCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");
        var now = clock.UtcNow;
        var today = clock.LocalToday();

        // Profil (rozilik, o'z ish soatlari), sozlamalar va bugungi qoidalar — "today" bilan bir manba.
        var practice = await db.LoadStudentPracticeAsync(userId, today, PeriodPurpose.Current, cancellationToken);
        var settings = practice.Settings;
        var consentGiven = practice.Student.HasTrackingConsent;

        var accepted = 0;
        if (settings.TrackingEnabled && consentGiven)
            accepted = await StoreAsync(userId, practice, request.Pings!, now, cancellationToken);

        var todayRow = practice.Period is null ? null : await db.AttendanceOnAsync(userId, today, track: false, cancellationToken);
        var tracking = practice.Period is null
            ? TrackingState.Inactive(settings, consentGiven)
            : TrackingState.Build(settings, consentGiven, todayRow, today, practice.Rules, now);

        var last = await db.LocationPings.AsNoTracking()
            .Where(p => p.StudentUserId == userId && p.Date == today)
            .OrderByDescending(p => p.OccurredAt)
            .Select(p => new LastPingDto(p.Status, p.DistanceM, p.OccurredAt))
            .FirstOrDefaultAsync(cancellationToken);

        return new LocationPingsResultDto(accepted, tracking, last is null ? null : last with { DistanceM = Math.Round(last.DistanceM) });
    }

    private async Task<int> StoreAsync(
        Guid userId, StudentPractice practice, IReadOnlyList<LocationPingInput?> pings, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var candidates = pings
            .Where(p => p is not null && IsValid(p))
            .Select(p => p! with { OccurredAt = LocationTracking.NormalizeInstant(p.OccurredAt) })
            .Where(p => LocationTracking.IsWithinAcceptWindow(p.OccurredAt, now))
            .DistinctBy(p => p.OccurredAt)
            .ToList();
        if (candidates.Count == 0)
            return 0;

        // 24 soatlik oyna ko'pi bilan 2 kunni qamraydi (Toshkent) — har kunning sessiyasi o'z davomat yozuvidan.
        var dates = candidates.Select(p => PracticeTime.LocalDate(p.OccurredAt)).Distinct().ToList();
        var rows = await db.DailyAttendances.AsNoTracking()
            .Where(a => a.StudentUserId == userId && dates.Contains(a.Date) && a.CheckInAt != null)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
            return 0;

        var periodIds = rows.Select(r => r.PeriodId).Distinct().ToList();
        var periods = await db.PracticePeriods.AsNoTracking()
            .Where(p => periodIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);
        // Korxona — check-in'dagi kabi shu davrning tasdiqlangan arizasidan (o'chirilgan korxona → null → sessiya yo'q).
        var companies = (await db.PracticeApplications.AsNoTracking()
                .Include(a => a.Company)
                .Where(a => a.StudentUserId == userId && periodIds.Contains(a.PeriodId) && a.Status == ApplicationStatus.Approved)
                .OrderByDescending(a => a.DecidedAt)
                .ToListAsync(cancellationToken))
            .Where(a => a.Company is not null)
            .GroupBy(a => a.PeriodId)
            .ToDictionary(g => g.Key, g => g.First().Company);

        var sessions = new Dictionary<DateOnly, Session>();
        foreach (var row in rows)
        {
            if (!periods.TryGetValue(row.PeriodId, out var period) || !companies.TryGetValue(row.PeriodId, out var company))
                continue;
            var periodRules = period.Rules(practice.Settings.MinGpsAccuracyM);
            var rules = practice.Student.HoursOn(row.Date) is { } own ? periodRules.WithHours(own.Start, own.End) : periodRules;
            var end = row.CheckOutAt ?? TrackingState.AutoCloseMoment(row.Date, rules);
            sessions[row.Date] = new Session(row.Id, row.CheckInAt!.Value, end, company.Location, company.RadiusM, rules.MinAccuracyM);
        }

        var inSession = candidates
            .Select(p => (Ping: p, Date: PracticeTime.LocalDate(p.OccurredAt)))
            .Where(x => sessions.TryGetValue(x.Date, out var s) && x.Ping.OccurredAt >= s.Start && x.Ping.OccurredAt <= s.End)
            .ToList();
        if (inSession.Count == 0)
            return 0;

        var from = inSession.Min(x => x.Ping.OccurredAt);
        var to = inSession.Max(x => x.Ping.OccurredAt);

        // Bir talabaning parallel paketlari (qayta yuborish) navbatga turadi — takror tekshiruvi va yozish atomar,
        // unique (student_user_id, occurred_at) indeksi esa oxirgi kafolat.
        return await db.InTransactionAsync(async ct =>
        {
            await db.AcquireTransactionLockAsync($"location-pings:{userId}", ct);
            var existing = (await db.LocationPings.AsNoTracking()
                    .Where(p => p.StudentUserId == userId && p.OccurredAt >= from && p.OccurredAt <= to)
                    .Select(p => p.OccurredAt)
                    .ToListAsync(ct))
                .Select(LocationTracking.NormalizeInstant)
                .ToHashSet();

            var added = 0;
            foreach (var (ping, date) in inSession)
            {
                if (existing.Contains(ping.OccurredAt))
                    continue;
                var session = sessions[date];
                var location = new GeoPoint(ping.Lat, ping.Lng);
                db.LocationPings.Add(LocationPing.Record(
                    userId, session.AttendanceId, date, ping.OccurredAt, location, ping.AccuracyM,
                    session.CompanyLocation.DistanceMetersTo(location), session.RadiusM, session.MinAccuracyM,
                    ping.IsMocked, ping.Source ?? LocationPingSource.Background, now));
                added++;
            }

            if (added > 0)
                await db.SaveChangesAsync(ct);
            return added;
        }, cancellationToken);
    }

    private static bool IsValid(LocationPingInput ping)
        => double.IsFinite(ping.Lat) && ping.Lat is >= -90 and <= 90
           && double.IsFinite(ping.Lng) && ping.Lng is >= -180 and <= 180
           && double.IsFinite(ping.AccuracyM) && ping.AccuracyM >= 0
           && (ping.Source is null || Enum.IsDefined(ping.Source.Value));

    private sealed record Session(
        Guid AttendanceId, DateTimeOffset Start, DateTimeOffset End, GeoPoint CompanyLocation, int RadiusM, double MinAccuracyM);
}
