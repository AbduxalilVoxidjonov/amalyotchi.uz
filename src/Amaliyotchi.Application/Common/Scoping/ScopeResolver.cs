using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Common.Scoping;

/// <summary>Tyutor ko'lami faol (<c>IsActive</c>, o'chirilmagan) biriktiruvlardan hisoblanadi;
/// talabalar ro'yxati (≤ bir necha yuz) bir marta yuklanadi — keyin barcha so'rovlar
/// <c>= ANY(@ids)</c> bilan ishlaydi, JOIN'siz.</summary>
internal sealed class ScopeResolver(IApplicationDbContext db, ICurrentUser currentUser) : IScopeResolver
{
    private DataScope? _cached;

    public async Task<DataScope> ResolveAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
            return _cached;

        _cached = await BuildAsync(cancellationToken);
        return _cached;
    }

    private async Task<DataScope> BuildAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
            return DataScope.Empty;

        switch (currentUser.Role)
        {
            case UserRole.Admin:
                return DataScope.Unrestricted(userId);

            case UserRole.Student:
                return DataScope.ForStudent(userId);

            case UserRole.Tutor:
                var groupIds = await db.TutorAssignments
                    .AsNoTracking()
                    .Where(a => a.TutorUserId == userId && a.IsActive)
                    .Select(a => a.StudentGroupId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                if (groupIds.Count == 0)
                    return DataScope.ForTutor(userId, [], []);

                var studentUserIds = await db.StudentProfiles
                    .AsNoTracking()
                    .Where(p => groupIds.Contains(p.StudentGroupId))
                    .Select(p => p.UserId)
                    .ToListAsync(cancellationToken);

                return DataScope.ForTutor(userId, groupIds, studentUserIds);

            default:
                return DataScope.Empty;
        }
    }
}
