using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Application.Features.Student.Calendar;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Student;

[ApiController]
[Route("api/student/calendar")]
[Authorize(Policy = Policies.StudentOnly)]
public sealed class StudentCalendarController(ISender sender) : ControllerBase
{
    /// <summary>Oylik davomat kalendari. <c>month=YYYY-MM</c>; berilmasa — joriy oy.</summary>
    [HttpGet]
    [ProducesResponseType<CalendarMonthDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CalendarMonthDto>> Get([FromQuery] string? month, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetStudentCalendarQuery(month), cancellationToken));
}
