using Amaliyotchi.Application.Common.Interfaces;
using MediatR;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

/// <summary><c>GET /api/admin/tutors/{id}</c> → <see cref="TutorDetail"/>. Topilmasa yoki roli tyutor bo'lmasa → 404.</summary>
public sealed record GetTutorQuery(Guid Id) : IRequest<TutorDetail>;

internal sealed class GetTutorQueryHandler(IApplicationDbContext db) : IRequestHandler<GetTutorQuery, TutorDetail>
{
    public Task<TutorDetail> Handle(GetTutorQuery request, CancellationToken cancellationToken)
        => TutorDetailQueries.LoadAsync(db, request.Id, cancellationToken);
}
