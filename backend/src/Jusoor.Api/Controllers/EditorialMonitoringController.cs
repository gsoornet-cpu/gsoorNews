using Jusoor.Application.Administration;
using Jusoor.Application.Common.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jusoor.Api.Controllers;

[ApiController]
[Route("api/v1/cms/reports/editorial")]
[Authorize(Policy = AuthorizationPolicies.CanViewEditorialMonitoring)]
public sealed class EditorialMonitoringController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        if (days is not (7 or 30 or 90)) return BadRequest(new { message = "days must be 7, 30, or 90." });
        return Ok(await mediator.Send(new GetEditorialMonitoringReportQuery(days), cancellationToken));
    }
}
