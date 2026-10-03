using System.Globalization;
using System.Text.Json;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Practice;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Students;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Student.Profile;

/// <summary>Talabaning ish vaqti (kelish/ketish) — <c>GET /api/student/profile</c> dagi <c>workHours</c> va
/// <c>PUT /api/student/profile/work-hours</c> javobi. Barcha vaqtlar "HH:mm" (Toshkent).</summary>
/// <param name="Start">Talabaning oxirgi saqlagan o'z kelish vaqti; null — davr soatlari ishlatiladi.</param>
/// <param name="End">Talabaning oxirgi saqlagan o'z ketish vaqti; null — davr soatlari.</param>
/// <param name="EffectiveFrom"><c>start/end</c> shu sanadan (yyyy-MM-dd) amal qiladi; hech qachon o'rnatilmagan bo'lsa null.
/// O'zgarish har doim ERTADAN kuchga kiradi.</param>
/// <param name="TodayStart">Bugun amaldagi kelish vaqti (o'zi belgilagan yoki davr/standart).</param>
/// <param name="TodayEnd">Bugun amaldagi ketish vaqti.</param>
/// <param name="PeriodStart">Sukut bo'yicha davr soati; davr yo'q → null.</param>
/// <param name="PeriodEnd">Sukut bo'yicha davr tugash soati; davr yo'q → null.</param>
public sealed record StudentWorkHoursDto(
    string? Start,
    string? End,
    DateOnly? EffectiveFrom,
    string TodayStart,
    string TodayEnd,
    string? PeriodStart,
    string? PeriodEnd);

internal static class StudentWorkHoursMapper
{
    /// <summary>Profil + sukut bo'yicha davr (<see cref="PeriodPurpose.Default"/>) dan DTO. Bugungi soatlar —
    /// <see cref="StudentPractice.Rules"/> (check-in/check-out bilan bir xil manba).</summary>
    public static StudentWorkHoursDto ToDto(StudentPractice practice)
    {
        var student = practice.Student;
        var rules = practice.Rules;
        return new StudentWorkHoursDto(
            student.WorkStart is { } start ? PracticeTime.Hm(start) : null,
            student.WorkEnd is { } end ? PracticeTime.Hm(end) : null,
            student.WorkHoursEffectiveFrom,
            PracticeTime.Hm(rules.DailyStart),
            PracticeTime.Hm(rules.DailyEnd),
            practice.Period is { } period ? PracticeTime.Hm(period.DailyStart) : null,
            practice.Period is { } p ? PracticeTime.Hm(p.DailyEnd) : null);
    }

    public static async Task<StudentWorkHoursDto> LoadAsync(
        IApplicationDbContext db, Guid userId, DateOnly today, CancellationToken cancellationToken)
        => ToDto(await db.LoadStudentPracticeAsync(userId, today, PeriodPurpose.Default, cancellationToken));
}

/// <summary><c>PUT /api/student/profile/work-hours</c>: <c>{ start: "HH:mm"|null, end: "HH:mm"|null }</c> → 200
/// <see cref="StudentWorkHoursDto"/>. Ikkalasi null — davr soatlariga qaytish. O'zgarish ERTADAN (Toshkent sanasi)
/// kuchga kiradi — bugungi "kech keldi" chegarasini surib bo'lmaydi (<see cref="StudentProfile.SetWorkHours"/>).
/// Format xatosi, faqat bittasi berilgan, ketish ≤ kelish yoki 1 soatdan qisqa → 400 (<c>errors.Start</c>/<c>errors.End</c>).
/// Audit: <see cref="AuditAction.StudentWorkHoursChanged"/>.</summary>
public sealed record SetStudentWorkHoursCommand(string? Start, string? End) : IRequest<StudentWorkHoursDto>;

internal sealed class SetStudentWorkHoursCommandHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IAuditWriter audit)
    : IRequestHandler<SetStudentWorkHoursCommand, StudentWorkHoursDto>
{
    public async Task<StudentWorkHoursDto> Handle(SetStudentWorkHoursCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");
        var today = clock.LocalToday();

        var profile = await db.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Talaba profili topilmadi.");

        // Validator formatni tekshirgan — bu yerda faqat parse.
        var start = StudentWorkHoursRules.Parse(request.Start);
        var end = StudentWorkHoursRules.Parse(request.End);

        var from = new { start = Hm(profile.WorkStart), end = Hm(profile.WorkEnd) };
        profile.SetWorkHours(start, end, today);

        await audit.WriteAsync(
            AuditAction.StudentWorkHoursChanged, nameof(StudentProfile), profile.Id.ToString(),
            changes: JsonSerializer.Serialize(new
            {
                from,
                to = new { start = Hm(start), end = Hm(end) },
                effectiveFrom = profile.WorkHoursEffectiveFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            }),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return await StudentWorkHoursMapper.LoadAsync(db, userId, today, cancellationToken);
    }

    private static string? Hm(TimeOnly? time) => time is { } t ? PracticeTime.Hm(t) : null;
}

/// <summary>Ish vaqti maydonlari qoidalari va xabarlari (validator va testlar uchun umumiy).</summary>
public static class StudentWorkHoursRules
{
    public const string TimeFormatMessage = "Vaqtni HH:mm formatida kiriting.";
    public const string StartRequiredMessage = "Kelish vaqtini kiriting.";
    public const string EndRequiredMessage = "Ketish vaqtini kiriting.";
    public const string EndBeforeStartMessage = "Ketish vaqti kelish vaqtidan keyin bo'lishi kerak.";
    public const string TooShortMessage = "Ish vaqti kamida 1 soat bo'lishi kerak.";

    private static readonly string[] TimeFormats = ["HH:mm", "H:mm"];

    public static bool TryParse(string? value, out TimeOnly time)
        => TimeOnly.TryParseExact(value?.Trim(), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    /// <summary>Bo'sh → null; aks holda "HH:mm" (validator o'tkazgan qiymat).</summary>
    public static TimeOnly? Parse(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : TryParse(value, out var time) ? time : throw new DomainException(TimeFormatMessage);
}

public sealed class SetStudentWorkHoursCommandValidator : AbstractValidator<SetStudentWorkHoursCommand>
{
    public SetStudentWorkHoursCommandValidator()
    {
        RuleFor(x => x.Start)
            .Cascade(CascadeMode.Stop)
            .Must(v => string.IsNullOrWhiteSpace(v) || StudentWorkHoursRules.TryParse(v, out _))
            .WithMessage(StudentWorkHoursRules.TimeFormatMessage)
            .Must((x, v) => !string.IsNullOrWhiteSpace(v) || string.IsNullOrWhiteSpace(x.End))
            .WithMessage(StudentWorkHoursRules.StartRequiredMessage);

        RuleFor(x => x.End)
            .Cascade(CascadeMode.Stop)
            .Must(v => string.IsNullOrWhiteSpace(v) || StudentWorkHoursRules.TryParse(v, out _))
            .WithMessage(StudentWorkHoursRules.TimeFormatMessage)
            .Must((x, v) => !string.IsNullOrWhiteSpace(v) || string.IsNullOrWhiteSpace(x.Start))
            .WithMessage(StudentWorkHoursRules.EndRequiredMessage)
            .Must((x, v) => !Both(x, out var start, out var end) || end > start)
            .WithMessage(StudentWorkHoursRules.EndBeforeStartMessage)
            .Must((x, v) => !Both(x, out var start, out var end)
                            || (end.ToTimeSpan() - start.ToTimeSpan()).TotalMinutes >= StudentProfile.MinWorkMinutes)
            .WithMessage(StudentWorkHoursRules.TooShortMessage);
    }

    private static bool Both(SetStudentWorkHoursCommand x, out TimeOnly start, out TimeOnly end)
    {
        end = default;
        return StudentWorkHoursRules.TryParse(x.Start, out start) & StudentWorkHoursRules.TryParse(x.End, out end);
    }
}
