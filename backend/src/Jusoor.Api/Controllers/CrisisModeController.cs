using Jusoor.Application.Administration;
using Jusoor.Application.Common.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jusoor.Api.Controllers;

[ApiController]
[Route("api/v1/crisis")]
public sealed class CrisisModeController(ISender mediator) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublic(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetPublicCrisisModeQuery(), cancellationToken));
}

[ApiController]
[Route("api/v1/cms/crisis")]
[Authorize(Policy = AuthorizationPolicies.CanManageCrisisMode)]
public sealed class CmsCrisisModeController(ISender mediator) : ControllerBase
{
    public sealed record SaveRequest(bool IsActive, string Title, string Summary, string SourceName, string SourceUrl, string Severity, IReadOnlyList<Guid>? AffectedCountryIds);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetCrisisModeQuery(), cancellationToken));

    [HttpPut]
    public async Task<IActionResult> Save(SaveRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SaveCrisisModeCommand(request.IsActive, request.Title, request.Summary, request.SourceName, request.SourceUrl,
            request.Severity, request.AffectedCountryIds ?? Array.Empty<Guid>()), cancellationToken);
        return Ok(result);
    }
}
