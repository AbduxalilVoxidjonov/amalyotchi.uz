using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Student.Messages;

/// <summary>Talaba ilova qutisidagi xabar. <paramref name="Id"/> — xabar id'si (<c>BroadcastMessage.Id</c>);
/// <paramref name="CreatedAt"/> — admin yuborgan vaqt; <paramref name="ReadAt"/> — talaba o'qigan vaqt (null — o'qilmagan).</summary>
public sealed record StudentMessageDto(Guid Id, string Text, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);

/// <summary><c>GET /api/student/messages/unread-count</c> javobi.</summary>
public sealed record StudentUnreadCountDto(int Count);

/// <summary>Talaba qutisi manbasi: faqat joriy talabaga yaratilgan yetkazishlar (har qabul qiluvchiga bitta qator —
/// Telegram holatidan qat'i nazar, <c>appOnly</c> ham). Xabar yaratilgan zahoti ko'rinadi.</summary>
internal static class StudentInbox
{
    public const string NotFound = "Xabar topilmadi.";

    public static Guid RequireUser(ICurrentUser currentUser)
        => currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");
}

/// <summary><c>GET /api/student/messages?page&amp;pageSize</c> (<c>pageSize</c> ≤ 100) — talabaning xabarlari, yangi birinchi.</summary>
public sealed record GetStudentMessagesQuery : PagedQuery, IRequest<Paged<StudentMessageDto>>;

public sealed class GetStudentMessagesQueryValidator : PagedQueryValidator<GetStudentMessagesQuery>;

internal sealed class GetStudentMessagesQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetStudentMessagesQuery, Paged<StudentMessageDto>>
{
    public Task<Paged<StudentMessageDto>> Handle(GetStudentMessagesQuery request, CancellationToken cancellationToken)
    {
        var userId = StudentInbox.RequireUser(currentUser);

        return (from d in db.BroadcastDeliveries.AsNoTracking()
                where d.RecipientUserId == userId
                join m in db.BroadcastMessages.AsNoTracking() on d.MessageId equals m.Id
                orderby m.CreatedAt descending, m.Id descending
                select new StudentMessageDto(m.Id, m.Text, m.CreatedAt, d.ReadAt))
            .ToPagedAsync(request, cancellationToken);
    }
}

/// <summary><c>GET /api/student/messages/unread-count</c> → <c>{ count }</c>.</summary>
public sealed record GetStudentUnreadCountQuery : IRequest<StudentUnreadCountDto>;

internal sealed class GetStudentUnreadCountQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetStudentUnreadCountQuery, StudentUnreadCountDto>
{
    public async Task<StudentUnreadCountDto> Handle(GetStudentUnreadCountQuery request, CancellationToken cancellationToken)
    {
        var userId = StudentInbox.RequireUser(currentUser);
        var count = await db.BroadcastDeliveries
            .AsNoTracking()
            .CountAsync(d => d.RecipientUserId == userId && d.ReadAt == null, cancellationToken);
        return new StudentUnreadCountDto(count);
    }
}

/// <summary><c>POST /api/student/messages/{id}/read</c> → 204. Idempotent (birinchi o'qilgan vaqt saqlanadi);
/// talabaga yuborilmagan yoki mavjud bo'lmagan xabar → 404.</summary>
public sealed record MarkStudentMessageReadCommand(Guid Id) : IRequest;

internal sealed class MarkStudentMessageReadCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<MarkStudentMessageReadCommand>
{
    public async Task Handle(MarkStudentMessageReadCommand request, CancellationToken cancellationToken)
    {
        var userId = StudentInbox.RequireUser(currentUser);
        var delivery = await db.BroadcastDeliveries
            .FirstOrDefaultAsync(d => d.MessageId == request.Id && d.RecipientUserId == userId, cancellationToken)
            ?? throw new NotFoundException(StudentInbox.NotFound);

        if (delivery.ReadAt is not null)
            return;

        delivery.MarkRead(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary><c>POST /api/student/messages/read-all</c> → 204: talabaning barcha o'qilmagan xabarlari bitta <c>UPDATE</c> bilan.
/// <c>BroadcastDelivery</c> audit'dan istisno — interceptor'ni chetlab o'tish xavfsiz.</summary>
public sealed record MarkAllStudentMessagesReadCommand : IRequest;

internal sealed class MarkAllStudentMessagesReadCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<MarkAllStudentMessagesReadCommand>
{
    public async Task Handle(MarkAllStudentMessagesReadCommand request, CancellationToken cancellationToken)
    {
        var userId = StudentInbox.RequireUser(currentUser);
        var now = clock.UtcNow;
        await db.BroadcastDeliveries
            .Where(d => d.RecipientUserId == userId && d.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.ReadAt, (DateTimeOffset?)now), cancellationToken);
    }
}
