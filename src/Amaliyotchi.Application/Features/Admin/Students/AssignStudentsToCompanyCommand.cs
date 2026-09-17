using System.Text.Json;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Tutor.Common;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary>Biriktirilmagan talaba — sabab bilan.</summary>
public sealed record AssignCompanyError(Guid StudentId, string StudentName, string Message);

/// <summary>Ommaviy biriktirish hisoboti: <paramref name="Assigned"/> + <paramref name="Skipped"/> = <paramref name="Total"/>.</summary>
public sealed record AssignCompanyResult(
    int Total, int Assigned, int Skipped, string CompanyName, IReadOnlyList<AssignCompanyError> Errors);

/// <summary><c>POST /api/admin/students/assign-company</c>: <c>{ studentIds[], companyId }</c> → 200.
/// Admin talabalar ro'yxatidan bir nechtasini belgilab, ularni korxonaga BIRIKTIRADI: har biriga
/// tasdiqlangan ariza yaratiladi (talaba o'zi ariza bermaydi, tyutor moderatsiyasi ham talab qilinmaydi).
/// Korxona topilmasa → 404, faol bo'lmasa → 409. Alohida talaba biriktirilmasa — hisobotdagi sabab bilan
/// tashlab yuboriladi (qisman bajarilish).</summary>
public sealed record AssignStudentsToCompanyCommand(IReadOnlyList<Guid> StudentIds, Guid CompanyId)
    : IRequest<AssignCompanyResult>;

/// <summary>Biriktirilmagan talabalar uchun sabablar.</summary>
public static class AssignCompanyMessages
{
    public const string StudentNotFoundMessage = "Talaba topilmadi.";
    public const string InactiveMessage = "Talaba hisobi faol emas.";
    public const string NoPeriodMessage = "Guruhiga faol amaliyot davri biriktirilmagan.";
    public const string AlreadyHereMessage = "Allaqachon shu korxonaga biriktirilgan.";
    public const string PendingMessage = "Ko'rib chiqilmagan arizasi bor — avval tyutor qaror qabul qilsin.";
    public const string CompanyInactiveMessage = "Korxona faol emas — avval uni faollashtiring.";

    public static string OtherCompanyMessage(string company) =>
        $"Boshqa korxonaga biriktirilgan: {company}.";
}

internal sealed class AssignStudentsToCompanyCommandHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IAuditWriter audit)
    : IRequestHandler<AssignStudentsToCompanyCommand, AssignCompanyResult>
{
    /// <summary>Tyutor qarorining izohi — talaba profilida shu matn ko'rinadi.</summary>
    public const string DecisionComment = "Admin tomonidan biriktirildi.";

    public async Task<AssignCompanyResult> Handle(
        AssignStudentsToCompanyCommand request, CancellationToken cancellationToken)
    {
        var adminId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");

        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CompanyId, cancellationToken)
            ?? throw new NotFoundException("Korxona", request.CompanyId);

        if (!company.IsActive)
            throw new ConflictException(AssignCompanyMessages.CompanyInactiveMessage);

        var studentIds = request.StudentIds.Distinct().ToList();
        var today = clock.LocalToday();
        var now = clock.UtcNow;

        var students = await db.StudentProfiles
            .AsNoTracking()
            .Where(p => studentIds.Contains(p.UserId))
            .Select(p => new
            {
                p.UserId,
                p.StudentGroupId,
                p.User.FullName,
                p.User.IsActive
            })
            .ToListAsync(cancellationToken);

        var periods = await PeriodLookup.LoadAsync(db, today, cancellationToken);

        // Mavjud arizalar (barcha faol davrlar kesimida) — takror biriktirmaslik uchun.
        var periodIds = periods.PeriodIds.ToList();
        var existing = await db.PracticeApplications
            .Where(a => studentIds.Contains(a.StudentUserId) && periodIds.Contains(a.PeriodId))
            .Include(a => a.Company)
            .ToListAsync(cancellationToken);

        var errors = new List<AssignCompanyError>();
        var assigned = 0;

        foreach (var studentId in studentIds)
        {
            var student = students.FirstOrDefault(s => s.UserId == studentId);
            if (student is null)
            {
                errors.Add(new AssignCompanyError(studentId, "—", AssignCompanyMessages.StudentNotFoundMessage));
                continue;
            }

            if (!student.IsActive)
            {
                errors.Add(new AssignCompanyError(studentId, student.FullName, AssignCompanyMessages.InactiveMessage));
                continue;
            }

            var period = periods.ForGroup(student.StudentGroupId);
            if (period is null)
            {
                errors.Add(new AssignCompanyError(studentId, student.FullName, AssignCompanyMessages.NoPeriodMessage));
                continue;
            }

            var periodId = period.Period.Id;
            var current = existing
                .Where(a => a.StudentUserId == studentId && a.PeriodId == periodId)
                .OrderBy(a => a.Status == ApplicationStatus.Approved ? 0 : 1)
                .ThenByDescending(a => a.SubmittedAt)
                .FirstOrDefault();

            if (current is { Status: ApplicationStatus.Approved or ApplicationStatus.Completed })
            {
                errors.Add(new AssignCompanyError(
                    studentId, student.FullName,
                    current.CompanyId == company.Id
                        ? AssignCompanyMessages.AlreadyHereMessage
                        : AssignCompanyMessages.OtherCompanyMessage(current.Company.Name)));
                continue;
            }

            if (current is { Status: ApplicationStatus.Submitted or ApplicationStatus.RevisionNeeded })
            {
                errors.Add(new AssignCompanyError(studentId, student.FullName, AssignCompanyMessages.PendingMessage));
                continue;
            }

            var application = PracticeApplication.Create(
                studentId, periodId, company.Id, company.RadiusM, contractFileId: null, submittedAt: now);
            application.Approve(adminId, company.RadiusM, [], DecisionComment, now);
            db.PracticeApplications.Add(application);
            assigned++;
        }

        if (assigned > 0)
        {
            await audit.WriteAsync(
                AuditAction.StudentsAssignedToCompany, nameof(Domain.Companies.Company), company.Id.ToString(),
                changes: JsonSerializer.Serialize(new
                {
                    company = company.Name,
                    total = studentIds.Count,
                    assigned,
                    skipped = studentIds.Count - assigned
                }),
                cancellationToken: cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
        }

        return new AssignCompanyResult(
            studentIds.Count, assigned, studentIds.Count - assigned, company.Name, errors);
    }
}

public sealed class AssignStudentsToCompanyCommandValidator : AbstractValidator<AssignStudentsToCompanyCommand>
{
    /// <summary>Bir so'rovda ko'pi bilan shuncha talaba (ro'yxat sahifasi 100 tagacha).</summary>
    public const int MaxStudents = 200;

    public const string StudentsRequiredMessage = "Kamida bitta talabani belgilang.";
    public const string CompanyRequiredMessage = "Korxonani tanlang.";
    public static readonly string TooManyMessage = $"Bir vaqtda ko'pi bilan {MaxStudents} ta talabani biriktirish mumkin.";

    public AssignStudentsToCompanyCommandValidator()
    {
        RuleFor(x => x.StudentIds)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(StudentsRequiredMessage)
            .Must(ids => ids.Distinct().Count() <= MaxStudents).WithMessage(TooManyMessage);

        RuleFor(x => x.CompanyId).NotEmpty().WithMessage(CompanyRequiredMessage);
    }
}
