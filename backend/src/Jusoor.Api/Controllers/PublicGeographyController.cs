using Jusoor.Application.Geography;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jusoor.Api.Controllers;

/// <summary>Public, minimal country/city catalog used by Atmaen and location selectors.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/geography")]
public sealed class PublicGeographyController(ISender mediator) : ControllerBase
{
    [HttpGet("countries")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCountries(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetPublicCountriesQuery(), cancellationToken));

    [HttpGet("countries/{countryId:guid}/cities")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCities(Guid countryId, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetPublicCitiesQuery(countryId), cancellationToken));
}
