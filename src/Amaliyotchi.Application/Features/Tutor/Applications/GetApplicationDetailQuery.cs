using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Features.Tutor.Common;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.Applications;

/// <summary><c>GET /api/tutor/applications/{id}</c>. Ko'lamdan tashqari → 404.</summary>
public sealed record GetApplicationDetailQuery(Guid Id) : IRequest<ApplicationDetail>;

internal sealed class GetApplicationDetailQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver)
    : IRequestHandler<GetApplicationDetailQuery, ApplicationDetail>
{
    public async Task<ApplicationDetail> Handle(GetApplicationDetailQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);

        var row = await db.PracticeApplications.AsNoTracking().InScope(scope)
            .Where(a => a.Id == request.Id)
            .Join(db.StudentProfiles, a => a.StudentUserId, p => p.UserId, (a, p) => new
            {
                Application = a,
                Company = a.Company,
                StudentName = p.User.FullName,
                GroupName = p.Group.Name,
                p.Group.Course,
                p.HemisId
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Ariza", request.Id);

        ApplicationContract? contract = null;
        if (row.Application.ContractFileId is { } fileId)
        {
            var file = await db.StoredFiles.AsNoTracking()
                .Where(f => f.Id == fileId)
                .Select(f => new { f.FileName, f.Pages, f.SizeBytes })
                .FirstOrDefaultAsync(cancellationToken);
            if (file is not null)
                contract = new ApplicationContract(file.FileName, file.Pages, file.SizeBytes, FileUrls.For(fileId));
        }

        var a = row.Application;
        var c = row.Company;
        return new ApplicationDetail(
            a.Id, a.StudentUserId, row.StudentName, row.GroupName, row.Course, row.HemisId, c.Name,
            a.Status, a.SubmittedAt, a.DecidedAt,
            new GeoCoords(c.Location.Latitude, c.Location.Longitude),
            a.ProposedRadiusM,
            new ApplicationCompany(c.Name, c.Tin, c.Activity, c.Address, c.SupervisorName, c.SupervisorPhone, c.MentorName, c.MentorPhone),
            contract,
            a.DecisionComment,
            a.Checklist,
            a.RevisionCount);
    }
}
