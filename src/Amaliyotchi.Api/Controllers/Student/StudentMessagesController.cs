using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Student.Messages;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Student;

/// <summary>Talabaning xabarlar qutisi (mobil ilova): admin "Xabarlar" bo'limidan yuborgan xabarlar. Telegram bog'langan-
/// bog'lanmaganidan qat'i nazar har bir qabul qiluvchi talabaning qutisiga tushadi; talaba faqat o'ziga yuborilganlarni ko'radi.</summary>
[ApiController]
[Route("api/student/messages")]
[Authorize(Policy = Policies.StudentOnly)]
public sealed class StudentMessagesController(ISender sender) : ControllerBase
{
    /// <summary>Xabarlar, yangi birinchi: <c>?page=1&amp;pageSize=20</c> (<c>pageSize</c> ≤ 100).</summary>
    [HttpGet]
    [ProducesResponseType<Paged<StudentMessageDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<Paged<StudentMessageDto>>> List(
        [FromQuery] GetStudentMessagesQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query, cancellationToken));

    /// <summary>O'qilmagan xabarlar soni: <c>{ count }</c>.</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType<StudentUnreadCountDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentUnreadCountDto>> UnreadCount(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetStudentUnreadCountQuery(), cancellationToken));

    /// <summary>Xabarni o'qilgan deb belgilash → 204 (idempotent). Talabaga yuborilmagan / mavjud bo'lmagan id → 404.</summary>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new MarkStudentMessageReadCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>Barcha xabarlarni o'qilgan deb belgilash → 204.</summary>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await sender.Send(new MarkAllStudentMessagesReadCommand(), cancellationToken);
        return NoContent();
    }
}
