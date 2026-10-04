using Amaliyotchi.Application.Common.Exceptions;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Faces;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Application.Features.Student.Today;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Faces;
using Amaliyotchi.Domain.Files;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Student.CheckIn;

/// <summary>Check-in va check-out urinishlarining umumiy qismi: talaba → faol davr → tasdiqlangan ariza → korxona →
/// masofa (Haversine) → QR mosligi → policy → selfi (bo'lsa) → <see cref="AttendanceEvent"/> (HAR urinish, rad etilgani ham) →
/// davomat → tranzaksiya. Rad etilganda hodisa avval saqlanadi, keyin xato tashlanadi
/// (Login'dagi <c>LoginFailedAsync</c> uslubi) — shuning uchun rad etilgan urinishning rasmi ham qoladi.
/// Yuzni tasdiqlash (<c>faceVerificationEnabled</c>, faqat check-in): selfi majburiy, tranzaksiyadan OLDIN tahlil qilinadi
/// (modellar yo'q → 503, hodisa yozilmaydi); policy qabul qilsa — etalon bilan solishtiriladi: etalon yo'q/rad etilgan →
/// <c>FaceNotEnrolled</c>, selfida yuz yo'q → <c>FaceNotDetected</c>, ball chegaradan past → <c>FaceMismatch</c>
/// (hammasi oddiy rad kabi hodisa + rasm bilan yoziladi, ball ham). Qabul qilinganda ball davomatga yoziladi.</summary>
internal sealed class AttendanceAttempt(
    IApplicationDbContext db, IClock clock, IFileStorage storage, Guid studentUserId, IFaceEngine? faceEngine = null)
{
    /// <summary>Urinish sharoiti — konkret qaror (check-in yoki check-out) shu asosda beriladi.</summary>
    public sealed record Situation(
        StudentPractice Practice,
        PracticePeriod Period,
        Company Company,
        CheckInRules Rules,
        DateOnly Today,
        TimeOnly LocalNow,
        DateTimeOffset ReceivedAt,
        GeoPoint Location,
        double DistanceM,
        bool QrValid,
        DailyAttendance? Attendance,
        bool HasApprovedLeave,
        IReadOnlyList<AttendanceEvent> RecentEvents);

    public async Task<TodayDto> RunAsync(
        IGeoRequest request,
        AttendanceEventKind kind,
        Func<Situation, CheckInVerdict> decide,
        Action<Situation, CheckInVerdict, AttendanceEvent> apply,
        CancellationToken cancellationToken)
    {
        // Sozlama yoqilgan bo'lsa rasmsiz yoki QR'siz urinish umuman qabul qilinmaydi (hodisa ham yozilmaydi):
        // 400, detail — birinchi aniq sabab, errors.Photo / errors.Qr.
        var settings = await EnsureRequiredPartsAsync(request, kind, cancellationToken);

        // Yuz tahlili — og'ir CPU ishi, shuning uchun tranzaksiya (qulflar) tashqarisida.
        FaceAnalysis? face = null;
        if (FaceCheckApplies(kind, settings))
        {
            if (faceEngine is null)
                throw new ServiceUnavailableException(FaceEngineExtensions.UnavailableMessage);
            face = await faceEngine.AnalyzeOrThrowAsync(request.Photo!, cancellationToken);
        }

        // Rasm saqlovchiga tranzaksiya ichida yoziladi; baza xatosida (tranzaksiya qaytsa) fayl o'chiriladi.
        // Rad etilgan urinish esa MUVAFFAQIYATLI commit — fayl qoladi, xato keyin tashlanadi.
        string? savedKey = null;
        CheckInVerdict verdict;
        try
        {
            verdict = await db.InTransactionAsync(async ct =>
            {
                var situation = await LoadAsync(request, ct);

                // Idempotentlik: bir xil occurredAt bilan qabul qilingan urinish allaqachon bor (offline navbat,
                // tarmoq takrori) → xato emas, mavjud natija. Takror rasm ham saqlanmaydi.
                if (situation.RecentEvents.Any(e => e.Kind == kind && e.Accepted && SameInstant(e.OccurredAt, request.OccurredAt)))
                    return CheckInVerdict.Accept();

                var result = decide(situation);
                if (result.Accepted && face is not null)
                    result = await VerifyFaceAsync(result, face, settings.FaceMatchThreshold, ct);
                var photoFileId = await SavePhotoAsync(request.Photo, situation.ReceivedAt, key => savedKey = key, ct);
                var attempt = AttendanceEvent.Record(
                    studentUserId, situation.Company.Id, situation.Today, kind,
                    request.OccurredAt, situation.ReceivedAt, situation.Location, request.Accuracy,
                    situation.DistanceM, situation.Company.RadiusM, result, photoFileId);
                db.AttendanceEvents.Add(attempt);

                if (result.Accepted)
                    apply(situation, result, attempt);

                await db.SaveChangesAsync(ct);
                return result;
            }, cancellationToken);
        }
        catch
        {
            if (savedKey is not null)
                await storage.DeleteAsync(savedKey, CancellationToken.None);
            throw;
        }

        if (!verdict.Accepted)
            throw verdict.ToException();

        return await TodayBuilder.BuildAsync(db, clock, studentUserId, cancellationToken);
    }

    public const string QrRequiredMessage = "Amaliyot joyidagi QR kodni skanerlang.";

    /// <summary>Yuzni tasdiqlash faqat check-in'ga qo'llanadi (check-out — yo'q).</summary>
    private static bool FaceCheckApplies(AttendanceEventKind kind, StudentSettings settings)
        => kind == AttendanceEventKind.CheckIn && settings.FaceVerificationEnabled;

    /// <summary>Policy qabul qilgan check-in'ni etalon yuz bilan tekshiradi. Etalon — pending yoki approved
    /// (tyutor ko'rmagan bo'lsa ham ishlatiladi).</summary>
    private async Task<CheckInVerdict> VerifyFaceAsync(
        CheckInVerdict accepted, FaceAnalysis face, int threshold, CancellationToken cancellationToken)
    {
        var reference = await db.StudentFaceEnrollments.AsNoTracking()
            .Where(f => f.StudentUserId == studentUserId)
            .Select(f => new { f.Status, f.Embedding })
            .FirstOrDefaultAsync(cancellationToken);
        if (reference is null || reference.Status is not (FaceEnrollmentStatus.Pending or FaceEnrollmentStatus.Approved))
            return CheckInVerdict.Reject(CheckInRejectReason.FaceNotEnrolled);
        if (face.Embedding is null)
            return CheckInVerdict.Reject(CheckInRejectReason.FaceNotDetected);

        var score = FaceMatch.Score(face.Embedding, reference.Embedding);
        return FaceMatch.IsMatch(score, threshold)
            ? accepted with { FaceMatchScore = score }
            : CheckInVerdict.Reject(CheckInRejectReason.FaceMismatch) with { FaceMatchScore = score };
    }

    private async Task<StudentSettings> EnsureRequiredPartsAsync(
        IGeoRequest request, AttendanceEventKind kind, CancellationToken cancellationToken)
    {
        var settings = await db.LoadStudentSettingsAsync(cancellationToken);
        var errors = new Dictionary<string, string[]>();
        string? detail = null;

        // Yuzni tasdiqlash yoqilgan bo'lsa check-in selfisi checkinPhotoRequired'dan qat'i nazar majburiy.
        if (request.Photo is null && (settings.CheckInPhotoRequired || FaceCheckApplies(kind, settings)))
        {
            var message = kind == AttendanceEventKind.CheckIn
                ? "Check-in uchun rasm majburiy."
                : "Check-out uchun rasm majburiy.";
            errors["Photo"] = [message];
            detail = message;
        }

        if (string.IsNullOrWhiteSpace(request.Qr) && settings.CheckInQrRequired)
        {
            errors["Qr"] = [QrRequiredMessage];
            detail ??= QrRequiredMessage;
        }

        if (detail is not null)
            throw new ValidationException(errors, detail);

        return settings;
    }

    /// <summary>Yuborilgan QR doim tekshiriladi (sozlama o'chiq bo'lsa ham); yuborilmagan bo'lsa — to'siq emas
    /// (majburiyligi <see cref="EnsureRequiredPartsAsync"/> da).</summary>
    private static bool IsQrValid(string? qr, Company company)
        => string.IsNullOrWhiteSpace(qr) || CheckInQr.Matches(qr, company.CheckInQrToken);

    /// <summary>Selfini saqlovchiga yozadi va <see cref="StoredFile"/> yaratadi (hali <c>SaveChanges</c> emas).
    /// Rasm yuborilmagan bo'lsa — null.</summary>
    private async Task<Guid?> SavePhotoAsync(
        UploadedFile? photo, DateTimeOffset receivedAt, Action<string> onSaved, CancellationToken cancellationToken)
    {
        if (photo is null)
            return null;

        await using var content = photo.OpenRead();
        var key = await storage.SaveAsync(content, photo.FileName, photo.ContentType, cancellationToken);
        onSaved(key);

        var stored = StoredFile.Create(
            StoredFileKind.CheckInPhoto, photo.FileName, photo.ContentType, photo.Length, key, receivedAt, studentUserId);
        db.StoredFiles.Add(stored);
        return stored.Id;
    }

    /// <summary>Bazada mikrosekund aniqlik — so'rovdagi 100 ns qismi yo'qoladi; 1 ms ichida — bir xil moment.</summary>
    private static bool SameInstant(DateTimeOffset a, DateTimeOffset b)
        => Math.Abs((a - b).Ticks) < TimeSpan.TicksPerMillisecond;

    private async Task<Situation> LoadAsync(IGeoRequest request, CancellationToken cancellationToken)
    {
        var receivedAt = clock.UtcNow;
        var today = clock.LocalToday();
        var localNow = clock.LocalTime();

        // Faqat davom etayotgan davr: ikki davr oralig'ida (yoki davr boshlanmagan/tugagan) urinish hodisasiz rad etiladi —
        // sabab nom va sana bilan ("Amaliyot davri hali boshlanmagan: <nom>, <sana> dan boshlanadi.").
        var practice = await db.LoadStudentPracticeAsync(studentUserId, today, PeriodPurpose.Ongoing, cancellationToken);
        if (practice.Period is null)
            throw new DomainException(practice.NoOngoingNote());

        // Korxona bo'lmasa hodisani yozib bo'lmaydi (CompanyId majburiy) — policy'dagi NotApproved xabari bilan rad.
        if (practice.Company is null)
            throw CheckInRejectReason.NotApproved.ToException();

        var location = new GeoPoint(request.Lat, request.Lng);
        var distance = practice.Company.Location.DistanceMetersTo(location);

        // Davomat qatori tranzaksiya ichida kuzatiladi — check-out uni o'zgartiradi.
        var attendance = await db.AttendanceOnAsync(studentUserId, today, track: true, cancellationToken);

        var periodId = practice.Period.Id;
        var hasLeave = await db.LeaveRequests
            .AsNoTracking()
            .AnyAsync(l => l.StudentUserId == studentUserId && l.PeriodId == periodId
                           && l.Status == Domain.Leave.LeaveRequestStatus.Approved
                           && l.DateFrom <= today && l.DateTo >= today, cancellationToken);

        // Shubha tahlili uchun: bugungi + so'nggi soatlardagi urinishlar va oldingi kunning qabul qilingan check-in'i.
        var since = receivedAt.AddHours(-SuspiciousDetector.SpeedWindowHours);
        var recent = await db.AttendanceEvents
            .AsNoTracking()
            .Where(e => e.StudentUserId == studentUserId && (e.Date == today || e.ReceivedAt >= since))
            .OrderByDescending(e => e.ReceivedAt)
            .Take(50)
            .ToListAsync(cancellationToken);
        var previousCheckIn = await db.AttendanceEvents
            .AsNoTracking()
            .Where(e => e.StudentUserId == studentUserId && e.Date < today && e.Accepted && e.Kind == AttendanceEventKind.CheckIn)
            .OrderByDescending(e => e.ReceivedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (previousCheckIn is not null && recent.All(e => e.Id != previousCheckIn.Id))
            recent.Add(previousCheckIn);

        return new Situation(
            practice, practice.Period, practice.Company, practice.Rules, today, localNow, receivedAt,
            location, distance, IsQrValid(request.Qr, practice.Company), attendance, hasLeave, recent);
    }
}
