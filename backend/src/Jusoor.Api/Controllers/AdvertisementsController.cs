using Jusoor.Application.Common.Security;
using Jusoor.Application.Editorial;
using Jusoor.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jusoor.Api.Controllers;

[ApiController]
[Route("api/v1/advertisements")]
public sealed class AdvertisementsController(ISender sender) : ControllerBase
{
    [HttpGet("placements")]
    [AllowAnonymous]
    public async Task<IActionResult> List([FromQuery] string surface, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<EditorialAdSurface>(surface, true, out var parsed) || !Enum.IsDefined(parsed))
            return BadRequest(new { message = "surface must be web or mobile." });
        return Ok(await sender.Send(new GetPublicAdPlacementsQuery(parsed), cancellationToken));
    }
}

[ApiController]
[Route("api/v1/cms/advertisements")]
[Authorize(Policy = AuthorizationPolicies.CanManageEditorialPublicationState)]
public sealed class CmsAdvertisementsController(ISender sender) : ControllerBase
{
    public sealed record SaveRequest(IReadOnlyList<EditorialAdPlacementInput>? Items);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetEditorialAdPlacementsQuery(), cancellationToken));

    [HttpPut]
    public async Task<IActionResult> Save(SaveRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SaveEditorialAdPlacementsCommand(request.Items ?? Array.Empty<EditorialAdPlacementInput>()), cancellationToken);
        return result.Succeeded ? Ok(result.Items) : BadRequest(new { message = "تأكد من اختيار صور جاهزة ومن صحة الروابط والمساحات." });
    }
}
