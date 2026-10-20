using Jusoor.Application.Common.Security;
using Jusoor.Application.Geography;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jusoor.Api.Controllers;

[ApiController]
[Route("api/v1/diaspora-map")]
[AllowAnonymous]
public sealed class DiasporaMapController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetPublicDiasporaMapQuery(), cancellationToken));
}

[ApiController]
[Route("api/v1/cms/diaspora-estimates")]
[Authorize(Policy = AuthorizationPolicies.CanManageGeography)]
public sealed class CmsDiasporaEstimatesController(ISender mediator) : ControllerBase
{
    public sealed record SaveRequest(Guid CountryId, Guid? CityId, int EstimatedPopulation, string SourceName,
        string SourceUrl, DateOnly SourcePublishedOn, bool IsPublished);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetDiasporaEstimatesQuery(), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(SaveRequest request, CancellationToken cancellationToken)
        => Ok(await mediator.Send(ToCommand(null, request), cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SaveRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(ToCommand(id, request), cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        => await mediator.Send(new DeleteDiasporaEstimateCommand(id), cancellationToken) ? NoContent() : NotFound();

    private static SaveDiasporaEstimateCommand ToCommand(Guid? id, SaveRequest request)
        => new(id, request.CountryId, request.CityId, request.EstimatedPopulation, request.SourceName,
            request.SourceUrl, request.SourcePublishedOn, request.IsPublished);
}
