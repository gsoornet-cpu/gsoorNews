using Jusoor.Application.Common.Security;
using Jusoor.Application.Geography;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jusoor.Api.Controllers;

[ApiController]
[Route("api/v1/cms/geography")]
[Authorize(Policy = AuthorizationPolicies.CanManageGeography)]
public sealed class CountryCatalogController(ISender mediator) : ControllerBase
{
    public sealed record CreateCountryRequest(string NameAr, string NameEn, string IsoCode2);
    public sealed record CreateCityRequest(Guid CountryId, string NameAr, string NameEn, string Slug, decimal? Latitude, decimal? Longitude);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetCountryCatalogQuery(), cancellationToken));

    [HttpPost("countries")]
    public async Task<IActionResult> CreateCountry(CreateCountryRequest request, CancellationToken cancellationToken)
    {
        var item = await mediator.Send(new CreateCountryCommand(request.NameAr, request.NameEn, request.IsoCode2), cancellationToken);
        return CreatedAtAction(nameof(Get), item);
    }

    [HttpPost("cities")]
    public async Task<IActionResult> CreateCity(CreateCityRequest request, CancellationToken cancellationToken)
    {
        var item = await mediator.Send(new CreateCityCommand(request.CountryId, request.NameAr, request.NameEn, request.Slug,
            request.Latitude, request.Longitude), cancellationToken);
        return Ok(item);
    }
}
