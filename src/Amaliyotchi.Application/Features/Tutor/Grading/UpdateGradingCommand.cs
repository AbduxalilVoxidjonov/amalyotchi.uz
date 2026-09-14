using System.Globalization;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Tutor.Common;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Grading;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.Grading;

/// <summary><c>PUT /api/tutor/grading/{studentId}</c> — <c>PracticeGrade</c> upsert (faol davr bo'yicha), javob qayta hisoblangan qator.
/// Ko'lamdan tashqari talaba → 404; faol davri yo'q → 400; yakunlangan baho → 409 (Domain).</summary>
public sealed record UpdateGradingCommand(Guid StudentId, int? TutorPoints, int? ReferencePoints) : IRequest<GradingRow>;

internal sealed class UpdateGradingCommandHandler(
    IApplicationDbContext db,
    IScopeResolver scopeResolver,
    IAuditWriter audit,
    IClock clock)
    : IRequestHandler<UpdateGradingCommand, GradingRow>
{
    public async Task<GradingRow> Handle(UpdateGradingCommand request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        var today = clock.LocalToday();

        var profile = await db.GetScopedStudentAsync(scope, request.StudentId, cancellationToken);
        var periods = await PeriodLookup.LoadAsync(db, today, cancellationToken);
        var period = periods.ForGroup(profile.StudentGroupId)
            ?? throw new DomainException("Talabaning faol amaliyot davri yo'q — baho qo'yib bo'lmaydi.");

        var grade = await db.PracticeGrades
            .FirstOrDefaultAsync(g => g.StudentUserId == profile.UserId && g.PeriodId == period.Period.Id, cancellationToken);

        var previous = grade is null ? "null" : Describe(grade.TutorPoints, grade.ReferencePoints);
        if (grade is null)
        {
            grade = PracticeGrade.Create(profile.UserId, period.Period.Id);
            db.PracticeGrades.Add(grade);
        }

        grade.SetTutorPoints(request.TutorPoints);
        grade.SetReferencePoints(request.ReferencePoints);

        await audit.WriteAsync(
            AuditAction.GradeChanged, nameof(PracticeGrade), grade.Id.ToString(),
            changes: $"{{\"from\":{previous},\"to\":{Describe(request.TutorPoints, request.ReferencePoints)}}}",
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var student = new ScopedStudent(
            profile.UserId, profile.User.FullName, profile.HemisId, profile.StudentGroupId,
            profile.Group.Name, profile.Group.Course, profile.Status);
        var rows = await GradingRowBuilder.BuildAsync(db, scope, [student], periods, today, clock.LocalTime(), cancellationToken);
        return rows.Single();
    }

    private static string Describe(int? tutorPoints, int? referencePoints)
        => string.Create(CultureInfo.InvariantCulture,
            $"{{\"tutorPoints\":{(tutorPoints?.ToString(CultureInfo.InvariantCulture) ?? "null")},\"referencePoints\":{(referencePoints?.ToString(CultureInfo.InvariantCulture) ?? "null")}}}");
}
