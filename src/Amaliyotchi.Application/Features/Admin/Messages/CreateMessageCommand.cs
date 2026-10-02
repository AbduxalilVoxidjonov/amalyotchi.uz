using System.Text.Json;
using Amaliyotchi.Application.Common.Exceptions;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Messaging;
using Amaliyotchi.Domain.Organization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ValidationException = Amaliyotchi.Application.Common.Exceptions.ValidationException;

namespace Amaliyotchi.Application.Features.Admin.Messages;

/// <summary><c>POST /api/admin/messages</c> → 201 <see cref="MessageSummary"/>. Auditoriya server tomonda resolve qilinadi
/// (shu paytdagi holat snapshot'i): faqat Telegram ulangan faol talabalar (<see cref="MessageRecipientQueries"/>);
/// <c>selected</c> dagi ulanmagan/mavjud bo'lmagan id'lar jim tashlanadi, takrorlar olib tashlanadi. Natija 0 → 400.
/// Har qabul qiluvchi uchun <see cref="BroadcastDelivery"/> (<c>pending</c>) — yuborishni fon dispetcheri bajaradi.</summary>
public sealed record CreateMessageCommand(string Text, bool AttachAppButton, MessageAudienceInput? Audience)
    : IRequest<MessageSummary>;

public sealed class CreateMessageCommandValidator : AbstractValidator<CreateMessageCommand>
{
    public const int MaxSelected = 5000;

    public CreateMessageCommandValidator()
    {
        RuleFor(x => x.Text)
            .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Xabar matnini kiriting.")
            .Must(t => t is null || t.Trim().Length <= BroadcastMessage.TextMaxLength)
            .WithMessage($"Xabar matni {BroadcastMessage.TextMaxLength} belgidan oshmasligi kerak.");

        RuleFor(x => x.Audience)
            .NotNull().WithMessage("Auditoriyani tanlang.");

        When(x => x.Audience is not null, () =>
        {
            RuleFor(x => x.Audience!.Kind)
                .IsInEnum().WithMessage("Auditoriya turi noto'g'ri.");

            RuleFor(x => x.Audience!.UserIds)
                .Must(ids => ids is { Count: > 0 }).WithMessage("Kamida bitta talabani tanlang.")
                .Must(ids => ids is null || ids.Count <= MaxSelected)
                .WithMessage($"Bir xabarda ko'pi bilan {MaxSelected} ta talaba tanlanadi.")
                .When(x => x.Audience!.Kind == BroadcastAudienceKind.Selected);

            RuleFor(x => x.Audience!.Q)
                .MaximumLength(PagedQueryValidator<GetMessageRecipientsQuery>.MaxQueryLength)
                .WithMessage($"Qidiruv matni {PagedQueryValidator<GetMessageRecipientsQuery>.MaxQueryLength} belgidan oshmasligi kerak.")
                .When(x => x.Audience!.Kind == BroadcastAudienceKind.Filter && x.Audience.Q is not null);

            RuleFor(x => x.Audience!.Course)
                .InclusiveBetween(StudentGroup.MinCourse, StudentGroup.MaxCourse)
                .WithMessage($"Kurs {StudentGroup.MinCourse} va {StudentGroup.MaxCourse} oralig'ida bo'lishi kerak.")
                .When(x => x.Audience!.Kind == BroadcastAudienceKind.Filter && x.Audience.Course is not null);
        });
    }
}

internal sealed class CreateMessageCommandHandler(IApplicationDbContext db, IAuditWriter audit, IClock clock)
    : IRequestHandler<CreateMessageCommand, MessageSummary>
{
    public const string EmptyAudience = "Tanlangan auditoriyada Telegram ulangan talaba yo'q.";

    /// <summary>Audit yozuviga matnning shuncha belgisi tushadi.</summary>
    public const int AuditTextPreview = 200;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<MessageSummary> Handle(CreateMessageCommand request, CancellationToken cancellationToken)
    {
        var audience = request.Audience!;
        var now = clock.UtcNow;

        var (recipients, label, audienceJson) = await ResolveAsync(audience, cancellationToken);
        if (recipients.Count == 0)
            throw new ValidationException(new Dictionary<string, string[]> { ["Audience"] = [EmptyAudience] }, EmptyAudience);

        var message = BroadcastMessage.Create(request.Text, request.AttachAppButton, audience.Kind, label, audienceJson);
        db.BroadcastMessages.Add(message);
        db.BroadcastDeliveries.AddRange(
            recipients.Select(r => BroadcastDelivery.Create(message.Id, r.UserId, r.ChatId, now)));

        var preview = message.Text.Length > AuditTextPreview ? message.Text[..AuditTextPreview] + "…" : message.Text;
        await audit.WriteAsync(
            AuditAction.BroadcastMessageCreated, nameof(BroadcastMessage), message.Id.ToString(),
            changes: JsonSerializer.Serialize(new
            {
                audience = label,
                recipients = recipients.Count,
                attachAppButton = message.AttachAppButton,
                textLength = message.Text.Length,
                text = preview
            }, Json),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return await MessageSummaries.LoadOneAsync(db, message.Id, cancellationToken);
    }

    private async Task<(List<Recipient> Recipients, string Label, string AudienceJson)> ResolveAsync(
        MessageAudienceInput audience, CancellationToken cancellationToken)
    {
        var source = MessageRecipientQueries.Source(db);

        switch (audience.Kind)
        {
            case BroadcastAudienceKind.Selected:
            {
                var ids = (audience.UserIds ?? []).Where(id => id != Guid.Empty).Distinct().ToList();
                var recipients = await Load(source.Where(x => ids.Contains(x.User.Id)), cancellationToken);
                return (recipients, AudienceLabel.Build(audience.Kind, recipients.Count),
                    JsonSerializer.Serialize(new { kind = "selected", requested = ids.Count }, Json));
            }

            case BroadcastAudienceKind.Filter:
            {
                var q = string.IsNullOrWhiteSpace(audience.Q) ? null : audience.Q.Trim();
                var filtered = MessageRecipientQueries.Filter(
                    source, q, audience.FacultyId, audience.DirectionId, audience.GroupId, audience.Course);
                var recipients = await Load(filtered, cancellationToken);

                var facultyName = audience.FacultyId is { } fId
                    ? await db.Faculties.AsNoTracking().Where(f => f.Id == fId).Select(f => f.Name).FirstOrDefaultAsync(cancellationToken)
                    : null;
                var directionName = audience.DirectionId is { } dId
                    ? await db.Directions.AsNoTracking().Where(d => d.Id == dId).Select(d => d.Name).FirstOrDefaultAsync(cancellationToken)
                    : null;
                var groupName = audience.GroupId is { } gId
                    ? await db.StudentGroups.AsNoTracking().Where(g => g.Id == gId).Select(g => g.Name).FirstOrDefaultAsync(cancellationToken)
                    : null;

                var label = AudienceLabel.Build(audience.Kind, recipients.Count, facultyName, directionName, audience.Course, groupName, q);
                var json = JsonSerializer.Serialize(new
                {
                    kind = "filter",
                    q,
                    audience.FacultyId,
                    audience.DirectionId,
                    audience.GroupId,
                    audience.Course
                }, Json);
                return (recipients, label, json);
            }

            default:
            {
                var recipients = await Load(source, cancellationToken);
                return (recipients, AudienceLabel.Build(BroadcastAudienceKind.All),
                    JsonSerializer.Serialize(new { kind = "all" }, Json));
            }
        }
    }

    private static async Task<List<Recipient>> Load(IQueryable<RecipientSource> source, CancellationToken cancellationToken)
    {
        var rows = await source
            .Select(x => new { x.User.Id, ChatId = x.User.TelegramUserId!.Value })
            .Distinct()
            .ToListAsync(cancellationToken);
        return rows.Select(r => new Recipient(r.Id, r.ChatId)).ToList();
    }

    private sealed record Recipient(Guid UserId, long ChatId);
}
