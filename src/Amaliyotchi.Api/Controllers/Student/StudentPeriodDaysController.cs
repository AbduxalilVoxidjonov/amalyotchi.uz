using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Student.PeriodDays;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Student;

[ApiController]
[Route("api/student/period-days")]
[Authorize(Policy = Policies.StudentOnly)]
public sealed class StudentPeriodDaysController(ISender sender) : ControllerBase
{
    /// <summary>Davrning har bir kuni (bosh ekran paneli). <c>periodId</c> berilmasa — today ko'rsatadigan davr;
    /// talabaga tegishli bo'lmagan <c>periodId</c> → 404.</summary>
    [HttpGet]
    [ProducesResponseType<StudentPeriodDaysDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentPeriodDaysDto>> Get([FromQuery] Guid? periodId, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetStudentPeriodDaysQuery(periodId), cancellationToken));
}
