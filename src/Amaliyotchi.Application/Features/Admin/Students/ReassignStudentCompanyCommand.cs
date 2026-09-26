using System.Text.Json;
using System.Text.Json.Serialization;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Application.Features.Tutor.Common;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Domain.Practice;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary><c>POST /api/admin/students/{id}/company</c>: <c>{ companyId, comment? }</c> → 200 <see cref="AdminStudentDetail"/>
/// (sukut davri bo'yicha — <c>GET /api/admin/students/{id}</c> bilan bir xil). Admin talaba profilidan uni korxonaga biriktiradi yoki boshqa korxonaga O'TKAZADI.
/// Davr — guruhning ariza davri (davom etayotgan → eng yaqin kelgusi, <see cref="PeriodPurpose.Enrollment"/>).
/// Talabaning shu davrdagi ochiq arizasi (yuborilgan / qaytarilgan / tasdiqlangan) <see cref="ApplicationStatus.Transferred"/>
/// holatiga o'tadi (tarix — davomat, kundalik, check-in'lar — unga bog'liq qoladi), so'ng yangi korxonaga tasdiqlangan
/// ariza yaratiladi. Ochiq ariza bo'lmasa — shunchaki tasdiqlangan ariza (birinchi biriktirish).
/// <see cref="Id"/> route'dan.</summary>
public sealed record ReassignStudentCompanyCommand(Guid CompanyId, string? Comment) : IRequest<AdminStudentDetail>
{
    [JsonIgnore]
    public Guid Id { get; init; }
}

public static class ReassignCompanyMessages
{
    public const string StudentInactiveMessage = "Talaba hisobi faol emas.";
    public const string NoPeriodMessage = "Talaba guruhiga faol amaliyot davri biriktirilmagan.";
    public const string AlreadyHereMessage = "Talaba allaqachon shu korxonaga biriktirilgan.";
    public const string CompanyInactiveMessage = AssignCompanyMessages.CompanyInactiveMessage;
}

internal sealed class ReassignStudentCompanyCommandHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IAuditWriter audit, ISender sender)
    : IRequestHandler<ReassignStudentCompanyCommand, AdminStudentDetail>
{
    public async Task<AdminStudentDetail> Handle(ReassignStudentCompanyCommand request, CancellationToken cancellationToken)
    {
        var adminId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");

        var student = await db.StudentProfiles
            .AsNoTracking()
            .Where(p => p.UserId == request.Id)
            .Select(p => new { p.UserId, p.StudentGroupId, p.User.FullName, p.User.IsActive })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Talaba", request.Id);

        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CompanyId, cancellationToken)
            ?? throw new NotFoundException("Korxona", request.CompanyId);

        if (!company.IsActive)
            throw new ConflictException(ReassignCompanyMessages.CompanyInactiveMessage);
        if (!student.IsActive)
            throw new ConflictException(ReassignCompanyMessages.StudentInactiveMessage);

        var today = clock.LocalToday();
        var periods = await PeriodLookup.LoadAsync(db, today, cancellationToken);
        var period = periods.ForGroup(student.StudentGroupId, PeriodPurpose.Enrollment)?.Period
            ?? throw new ConflictException(ReassignCompanyMessages.NoPeriodMessage);
        var periodId = period.Id;
        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();

        await db.InTransactionAsync(async ct =>
        {
            var now = clock.UtcNow;
            var applications = await db.PracticeApplications
                .Include(a => a.Company)
                .Where(a => a.StudentUserId == student.UserId && a.PeriodId == periodId)
                .ToListAsync(ct);

            var current = StudentPlacement.Current(applications);
            if (current is { Status: ApplicationStatus.Approved or ApplicationStatus.Completed }
                && current.CompanyId == company.Id)
                throw new ConflictException(ReassignCompanyMessages.AlreadyHereMessage);

            // Yakunlangan (Completed) ariza — tarix; uni o'tkazib bo'lmaydi.
            if (current is { Status: ApplicationStatus.Completed })
                throw new ConflictException("Talabaning bu davrdagi amaliyoti yakunlangan — korxonani o'zgartirib bo'lmaydi.");

            var previous = current is { IsOpen: true } ? current : null;
            var previousStatus = previous?.Status;
            if (previous is not null)
            {
                previous.Transfer(adminId, comment ?? $"Boshqa korxonaga o'tkazildi: {company.Name}.", now);
                // Unikal indeks (student+davr, tirik holatlar) — eski ariza avval yopilib saqlanadi, keyin yangisi qo'shiladi.
                await db.SaveChangesAsync(ct);
            }

            db.PracticeApplications.Add(
                StudentPlacement.CreateApproved(student.UserId, periodId, company, adminId, comment, now));

            await audit.WriteAsync(
                AuditAction.StudentCompanyReassigned, nameof(User), student.UserId.ToString(),
                changes: JsonSerializer.Serialize(new
                {
                    student = student.FullName,
                    periodId,
                    fromCompanyId = previous?.CompanyId,
                    fromCompany = previous?.Company.Name,
                    fromStatus = previousStatus is { } status ? JsonNamingPolicy.CamelCase.ConvertName(status.ToString()) : null,
                    toCompanyId = company.Id,
                    toCompany = company.Name
                }),
                reason: comment,
                cancellationToken: ct);

            await db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        // Javob — GET /api/admin/students/{id} (periodId'siz, sukut davri) bilan aynan bir xil.
        return await sender.Send(new GetAdminStudentDetailQuery(student.UserId), cancellationToken);
    }
}

public sealed class ReassignStudentCompanyCommandValidator : AbstractValidator<ReassignStudentCompanyCommand>
{
    public const int CommentMaxLength = 500;

    public const string CompanyRequiredMessage = AssignStudentsToCompanyCommandValidator.CompanyRequiredMessage;
    public static readonly string CommentTooLongMessage = $"Izoh {CommentMaxLength} belgidan oshmasligi kerak.";

    public ReassignStudentCompanyCommandValidator()
    {
        RuleFor(x => x.CompanyId).NotEmpty().WithMessage(CompanyRequiredMessage);
        RuleFor(x => x.Comment).MaximumLength(CommentMaxLength).WithMessage(CommentTooLongMessage);
    }
}
