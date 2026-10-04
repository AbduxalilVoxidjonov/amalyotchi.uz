using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Features.Faces;
using Amaliyotchi.Application.Features.Tutor.Common;
using Amaliyotchi.Domain.Faces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.Faces;

/// <summary><c>GET /api/tutor/face-enrollments?status=pending|approved|rejected</c> (sukut — <c>pending</c>) — ko'lamdagi
/// talabalarning etalon yuz rasmlari. Tartib: pending — eng eski yuborilgan birinchi (navbat), qolganlari — oxirgi ko'rib
/// chiqilgan birinchi. Sahifalanmaydi (tyutor ro'yxatlari kabi). Admin — barcha talabalar.</summary>
public sealed record GetFaceEnrollmentsQuery(FaceEnrollmentStatus? Status = null) : IRequest<FaceEnrollmentList>;

internal sealed class GetFaceEnrollmentsQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver)
    : IRequestHandler<GetFaceEnrollmentsQuery, FaceEnrollmentList>
{
    public async Task<FaceEnrollmentList> Handle(GetFaceEnrollmentsQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        var status = request.Status ?? FaceEnrollmentStatus.Pending;

        var query = db.ScopedFaceEnrollments(scope).Where(x => x.Enrollment.Status == status);
        query = status == FaceEnrollmentStatus.Pending
            ? query.OrderBy(x => x.Enrollment.SubmittedAt).ThenBy(x => x.Enrollment.Id)
            : query.OrderByDescending(x => x.Enrollment.ReviewedAt).ThenBy(x => x.Enrollment.Id);

        var rows = await query
            .Select(x => new
            {
                x.Profile.UserId,
                x.Profile.User.FullName,
                x.Profile.HemisId,
                Group = x.Profile.Group.Name,
                x.Enrollment.PhotoFileId,
                x.Enrollment.Status,
                x.Enrollment.SubmittedAt,
                x.Enrollment.ReviewedAt,
                x.Enrollment.RejectReason
            })
            .ToListAsync(cancellationToken);

        return new FaceEnrollmentList(rows
            .Select(r => new FaceEnrollmentItem(
                r.UserId, r.FullName, r.HemisId, r.Group, FileUrls.For(r.PhotoFileId),
                StudentFaceDto.ToStatus(r.Status), r.SubmittedAt, r.ReviewedAt, r.RejectReason))
            .ToList());
    }
}

/// <summary>Member-init (konstruktor emas) — EF keyingi Where/OrderBy/Select'da maydonlarga murojaatni SQL'ga o'gira olishi uchun.</summary>
internal sealed class ScopedFaceEnrollment
{
    public required StudentFaceEnrollment Enrollment { get; init; }
    public required Domain.Students.StudentProfile Profile { get; init; }
}

internal static class FaceEnrollmentScopeQueries
{
    /// <summary>Ko'lamdagi talabalar etalonlari (profil bilan birlashtirilgan — o'chirilgan profil ko'rinmaydi).
    /// Ro'yxat va nav hisobi bir manbadan.</summary>
    public static IQueryable<ScopedFaceEnrollment> ScopedFaceEnrollments(this IApplicationDbContext db, DataScope scope)
        => db.StudentFaceEnrollments.AsNoTracking()
            .Join(db.StudentProfiles.AsNoTracking().InScope(scope),
                f => f.StudentUserId, p => p.UserId,
                (f, p) => new ScopedFaceEnrollment { Enrollment = f, Profile = p });

    /// <summary>Nav badge: <c>GET /api/tutor/face-enrollments</c> sukut ro'yxati (pending) uzunligi.</summary>
    public static Task<int> CountPendingFaceEnrollmentsAsync(
        this IApplicationDbContext db, DataScope scope, CancellationToken cancellationToken)
        => db.ScopedFaceEnrollments(scope)
            .CountAsync(x => x.Enrollment.Status == FaceEnrollmentStatus.Pending, cancellationToken);
}
