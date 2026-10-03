using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Student.Profile;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Student;

[ApiController]
[Route("api/student/profile")]
[Authorize(Policy = Policies.StudentOnly)]
public sealed class StudentProfileController(ISender sender) : ControllerBase
{
    /// <summary>Talaba kabineti: akademik ma'lumot, biriktirilgan tyutor, Telegram/parol holati va sukut bo'yicha
    /// davrdagi amaliyot xulosasi (korxona, o'tgan ish kunlari, davomat, shubhali kunlar, joriy ball/baho).
    /// Davr yo'q bo'lsa <c>practice = null</c>.</summary>
    [HttpGet]
    [ProducesResponseType<StudentProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentProfileDto>> Get(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetStudentProfileQuery(), cancellationToken));

    /// <summary>Talaba o'z ish vaqtini belgilaydi: <c>{ start: "HH:mm"|null, end: "HH:mm"|null }</c> (ikkalasi null —
    /// davr soatlariga qaytish). O'zgarish ERTADAN kuchga kiradi. Format/oraliq xatosi → 400.</summary>
    [HttpPut("work-hours")]
    [ProducesResponseType<StudentWorkHoursDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentWorkHoursDto>> SetWorkHours(
        SetStudentWorkHoursCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command, cancellationToken));
}
