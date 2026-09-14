using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Application.Features.Student.CheckIn;
using Amaliyotchi.Application.Features.Student.CheckOut;
using Amaliyotchi.Application.Features.Student.Today;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Student;

/// <summary>Talabaning bugungi belgilanishi: holat, check-in, check-out. Ko'lam — faqat o'zi (Bearer'dan).</summary>
[ApiController]
[Route("api/student")]
[Authorize(Policy = Policies.StudentOnly)]
public sealed class StudentTodayController(ISender sender) : ControllerBase
{
    [HttpGet("today")]
    [ProducesResponseType<TodayDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TodayDto>> Today(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetStudentTodayQuery(), cancellationToken));

    /// <summary>Kelganini belgilash. 400 — oyna/ish kuni/GPS aniqligi; 409 — radius tashqarisi yoki allaqachon belgilangan.
    /// Rad etilgan urinish ham saqlanadi.</summary>
    [HttpPost("checkin")]
    [ProducesResponseType<TodayDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TodayDto>> CheckIn(CheckInCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command, cancellationToken));

    /// <summary>Ketganini belgilash. 400 — oyna/GPS; 409 — avval check-in kerak, allaqachon ketgan, radius tashqarisi.</summary>
    [HttpPost("checkout")]
    [ProducesResponseType<TodayDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TodayDto>> CheckOut(CheckOutCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command, cancellationToken));
}
