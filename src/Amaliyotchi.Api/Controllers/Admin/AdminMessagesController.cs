using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Admin.Messages;
using Amaliyotchi.Domain.Messaging;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

/// <summary>"Xabarlar": admin Telegram'ni bog'lagan talabalarga bot orqali xabar yuboradi — bittasiga, tanlanganlarga
/// yoki filtr bo'yicha ommaviy. Yuborishni fon dispetcheri bajaradi (DB-navbat, ≤ 25 xabar/s); bu yerda — navbatga
/// qo'yish, tarix va yetkazish holati.</summary>
[ApiController]
[Route("api/admin/messages")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminMessagesController(ISender sender) : ControllerBase
{
    /// <summary>Telegram ulangan faol talabalar (FISH bo'yicha): <c>q</c> — FISH, HEMIS ID yoki Telegram ID;
    /// <c>facultyId</c>, <c>directionId</c>, <c>groupId</c>, <c>course</c> — ixtiyoriy (AND). <c>pageSize</c> ≤ 500.</summary>
    [HttpGet("recipients")]
    [ProducesResponseType<Paged<MessageRecipientRow>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Paged<MessageRecipientRow>>> Recipients(
        [FromQuery] GetMessageRecipientsQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query, cancellationToken));

    /// <summary>Guruh tanlagichi: faqat Telegram ulangan talabasi bor guruhlar (<c>{ id, name }[]</c>), nom bo'yicha.</summary>
    [HttpGet("recipients/groups")]
    [ProducesResponseType<IReadOnlyList<MessageRecipientGroupOption>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<MessageRecipientGroupOption>>> RecipientGroups(
        [FromQuery] GetMessageRecipientGroupsQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query, cancellationToken));

    /// <summary>Yangi xabar: <c>{ text, attachAppButton, audience }</c> → 201 + Location. Matn 1..4000 belgi (trim);
    /// auditoriyada Telegram ulangan talaba bo'lmasa → 400.</summary>
    [HttpPost]
    [ProducesResponseType<MessageSummary>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<MessageSummary>> Create(CreateMessageCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary>Yuborilgan xabarlar tarixi — yangi birinchi.</summary>
    [HttpGet]
    [ProducesResponseType<Paged<MessageSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Paged<MessageSummary>>> List([FromQuery] GetMessagesQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query, cancellationToken));

    /// <summary>Bitta xabar va yetkazish sonlari. Topilmasa → 404.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<MessageSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageSummary>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetMessageQuery(id), cancellationToken));

    /// <summary>Xabarning yetkazishlari: <c>status</c> (pending|sent|failed|blocked) va <c>q</c> (FISH, HEMIS ID) — ixtiyoriy.</summary>
    [HttpGet("{id:guid}/deliveries")]
    [ProducesResponseType<Paged<MessageDeliveryRow>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Paged<MessageDeliveryRow>>> Deliveries(
        Guid id,
        [FromQuery] BroadcastDeliveryStatus? status,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Application.Common.Models.PagedQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => Ok(await sender.Send(
            new GetMessageDeliveriesQuery { Id = id, Status = status, Q = q, Page = page, PageSize = pageSize },
            cancellationToken));

    /// <summary><c>failed</c> yetkazishlarni qayta navbatga qo'yish (<c>blocked</c> emas) → 200 yangilangan xabar.</summary>
    [HttpPost("{id:guid}/retry")]
    [ProducesResponseType<MessageSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageSummary>> Retry(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new RetryMessageCommand(id), cancellationToken));
}
