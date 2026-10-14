using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Students;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary><c>PUT /api/admin/students/{id}/work-hours</c>: <c>{ start: "HH:mm"|null, end: "HH:mm"|null }</c> → 200
/// <see cref="AdminStudentDetail"/> (sukut davri bo'yicha — <c>GET /api/admin/students/{id}</c> bilan bir xil; yangi
/// qiymatlar <c>workHours</c> blokida). Talabaning ish vaqtini FAQAT admin belgilaydi (talaba ilovada o'zgartira olmaydi).
/// Ikkalasi null — davr soatlariga qaytish. O'zgarish ERTADAN (Toshkent sanasi) kuchga kiradi — bugungi check-in oynasi
/// va "kech keldi" chegarasi o'zgarmaydi (<see cref="StudentProfile.SetWorkHours"/>). Ko'lamdan tashqari / yo'q talaba → 404.
/// Format xatosi, faqat bittasi berilgan, ketish ≤ kelish yoki 1 soatdan qisqa → 400 (<c>errors.Start</c>/<c>errors.End</c>).
/// Audit: <see cref="AuditAction.StudentWorkHoursChanged"/>. <see cref="Id"/> route'dan.</summary>
public sealed record SetStudentWorkHoursCommand(string? Start, string? End) : IRequest<AdminStudentDetail>
{
    [JsonIgnore]
    public Guid Id { get; init; }
}

internal sealed class SetStudentWorkHoursCommandHandler(
    IApplicationDbContext db, IScopeResolver scopeResolver, IClock clock, IAuditWriter audit, ISender sender)
    : IRequestHandler<SetStudentWorkHoursCommand, AdminStudentDetail>
{
    public async Task<AdminStudentDetail> Handle(SetStudentWorkHoursCommand request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        var today = clock.LocalToday();

        // Ko'lamdan tashqari (yoki umuman yo'q) talaba → 404.
        var profile = await db.StudentProfiles.InScope(scope)
            .FirstOrDefaultAsync(p => p.UserId == request.Id, cancellationToken)
            ?? throw new NotFoundException("Talaba", request.Id);

        // Validator formatni tekshirgan — bu yerda faqat parse.
        var start = StudentWorkHoursRules.Parse(request.Start);
        var end = StudentWorkHoursRules.Parse(request.End);

        var from = new { start = Hm(profile.WorkStart), end = Hm(profile.WorkEnd) };
        profile.SetWorkHours(start, end, today);

        await audit.WriteAsync(
            AuditAction.StudentWorkHoursChanged, nameof(StudentProfile), profile.Id.ToString(),
            changes: JsonSerializer.Serialize(new
            {
                studentId = profile.UserId,
                from,
                to = new { start = Hm(start), end = Hm(end) },
                effectiveFrom = profile.WorkHoursEffectiveFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            }),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        // Javob — GET /api/admin/students/{id} (periodId'siz, sukut davri) bilan aynan bir xil.
        return await sender.Send(new GetAdminStudentDetailQuery(profile.UserId), cancellationToken);
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
