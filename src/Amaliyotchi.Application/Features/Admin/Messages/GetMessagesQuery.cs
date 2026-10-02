using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Messages;

/// <summary><c>GET /api/admin/messages?page&amp;pageSize</c> — yuborilgan xabarlar tarixi, yangi birinchi.</summary>
public sealed record GetMessagesQuery : PagedQuery, IRequest<Paged<MessageSummary>>;

public sealed class GetMessagesQueryValidator : PagedQueryValidator<GetMessagesQuery>;

internal sealed class GetMessagesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetMessagesQuery, Paged<MessageSummary>>
{
    public async Task<Paged<MessageSummary>> Handle(GetMessagesQuery request, CancellationToken cancellationToken)
    {
        var page = await db.BroadcastMessages
            .AsNoTracking()
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .ToPagedAsync(request, cancellationToken);

        if (page.Total == 0)
            return Paged<MessageSummary>.Empty(request);

        var items = await MessageSummaries.LoadAsync(db, page.Items, cancellationToken);
        return new Paged<MessageSummary>(items, page.Page, page.PageSize, page.Total);
    }
}

/// <summary><c>GET /api/admin/messages/{id}</c> → <see cref="MessageSummary"/>; topilmasa 404.</summary>
public sealed record GetMessageQuery(Guid Id) : IRequest<MessageSummary>;

internal sealed class GetMessageQueryHandler(IApplicationDbContext db) : IRequestHandler<GetMessageQuery, MessageSummary>
{
    public Task<MessageSummary> Handle(GetMessageQuery request, CancellationToken cancellationToken)
        => MessageSummaries.LoadOneAsync(db, request.Id, cancellationToken);
}
