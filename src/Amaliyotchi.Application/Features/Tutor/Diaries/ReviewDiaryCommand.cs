using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.Diaries;

/// <summary><c>POST /api/tutor/diaries/{id}/review</c>: <c>approve</c> (ball ixtiyoriy), <c>score</c> (ball majburiy),
/// <c>rewrite</c> (izoh majburiy). Allaqachon ko'rib chiqilgan → 409 (Domain).</summary>
public sealed record ReviewDiaryCommand(Guid DiaryId, DiaryReviewAction Action, int? Score, string? Comment)
    : IRequest<TutorDiaryEntry>;

internal sealed class ReviewDiaryCommandHandler(
    IApplicationDbContext db,
    IScopeResolver scopeResolver,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock)
    : IRequestHandler<ReviewDiaryCommand, TutorDiaryEntry>
{
    public async Task<TutorDiaryEntry> Handle(ReviewDiaryCommand request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        var userId = currentUser.UserId ?? throw new ForbiddenException();
        var now = clock.UtcNow;

        var entry = await db.DiaryEntries
            .InScope(scope)
            .FirstOrDefaultAsync(d => d.Id == request.DiaryId, cancellationToken)
            ?? throw new NotFoundException("Kundalik yozuvi", request.DiaryId);

        switch (request.Action)
        {
            case DiaryReviewAction.Approve:
                entry.Approve(userId, request.Score, request.Comment, now);
                break;
            case DiaryReviewAction.Score:
                entry.Approve(userId, request.Score ?? throw new DomainException("Ball ko'rsatilmagan."), request.Comment, now);
                break;
            case DiaryReviewAction.Rewrite:
                entry.RequestRewrite(userId, request.Comment ?? string.Empty, now);
                break;
            default:
                throw new DomainException("Noma'lum amal.");
        }

        await audit.WriteAsync(
            AuditAction.DiaryReviewed, nameof(DiaryEntry), entry.Id.ToString(),
            changes: $"{{\"status\":\"{entry.Status}\",\"score\":{(entry.Score is { } s ? s.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null")}}}",
            reason: request.Comment,
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var updated = await db.DiaryEntries.AsNoTracking()
            .Where(d => d.Id == entry.Id)
            .SelectTutorEntriesAsync(db, cancellationToken);
        return updated.Single();
    }
}
