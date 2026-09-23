using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Student.Place;

/// <summary><c>GET /api/student/place</c>. Faol davr yoki ariza yo'q → 404 "biriktirilmagan".</summary>
public sealed record GetPracticePlaceQuery : IRequest<PracticePlaceDto>;

internal sealed class GetPracticePlaceQueryHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<GetPracticePlaceQuery, PracticePlaceDto>
{
    public async Task<PracticePlaceDto> Handle(GetPracticePlaceQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");
        // Davom etayotgan → eng yaqin kelgusi (bahorgi davrga oldindan berilgan ariza) → oxirgi tugagan.
        var practice = await db.LoadStudentPracticeAsync(userId, clock.LocalToday(), PeriodPurpose.Current, cancellationToken);

        if (practice.Period is null || practice.Application is null || practice.Company is null)
            throw new NotFoundException("Amaliyot joyi hali biriktirilmagan.");

        var application = practice.Application;
        var company = practice.Company;

        PracticeContractDto? contract = null;
        if (application.ContractFileId is { } fileId)
        {
            var file = await db.StoredFiles.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fileId, cancellationToken);
            if (file is not null)
            {
                string? approvedBy = null;
                if (application.DecidedByUserId is { } deciderId)
                {
                    approvedBy = await db.Users.AsNoTracking()
                        .Where(u => u.Id == deciderId)
                        .Select(u => u.FullName)
                        .FirstOrDefaultAsync(cancellationToken);
                }

                var templateFileId = await db.DocumentTemplates.AsNoTracking()
                    .Where(t => t.Kind == DocumentTemplateKind.Contract && t.IsActive)
                    .OrderByDescending(t => t.CreatedAt)
                    .Select(t => (Guid?)t.FileId)
                    .FirstOrDefaultAsync(cancellationToken);

                contract = new PracticeContractDto(
                    file.Id, file.FileName, file.Pages, file.SizeBytes, file.UploadedAt,
                    application.Status == Domain.Practice.ApplicationStatus.Approved ? application.DecidedAt : null,
                    approvedBy,
                    templateFileId is { } tid ? StudentQueries.FileUrl(tid) : null);
            }
        }

        return new PracticePlaceDto(
            application.Status,
            application.DecisionComment,
            company.Name,
            company.Tin,
            company.Activity,
            company.Address,
            company.SupervisorName,
            company.SupervisorPhone,
            company.MentorName,
            company.MentorPhone,
            company.RadiusM,
            company.Location.Latitude,
            company.Location.Longitude,
            practice.Period.StartDate,
            practice.Period.EndDate,
            contract);
    }
}
