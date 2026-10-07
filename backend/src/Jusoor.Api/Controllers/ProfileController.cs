using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Profile;
using Jusoor.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jusoor.Api.Controllers;

/// <summary>
/// Spec §12's diaspora profile — a personal-data endpoint, not a newsroom
/// one: [Authorize] with no policy (any authenticated user acting on their
/// own account), unlike ReviewController's role-gated
/// [Authorize(Policy=...)]. RegisterCommand itself is deliberately
/// untouched — spec calls country-level privacy a "suggested default at
/// registration," not a required registration field, so the frontend can
/// nudge a new user toward setting it via this controller after signup
/// rather than this API forcing a location choice into the signup form.
/// </summary>
[ApiController]
[Route("api/v1/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ICurrentUserService _currentUser;

    public ProfileController(ISender mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    public record UpdateLocationRequest(DiasporaPrivacyLevel PrivacyLevel, Guid? CountryId, Guid? RegionId, Guid? CityId);
    public record AddPlaceRequest(Guid CityId);

    private IActionResult? RequireUser(out string userId)
    {
        userId = _currentUser.UserId!;
        return _currentUser.UserId is null ? Unauthorized() : null;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        if (RequireUser(out var userId) is { } unauthorized) return unauthorized;

        var result = await _mediator.Send(new GetMyProfileQuery(userId), cancellationToken);
        return Ok(result);
    }

    [HttpPut("location")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateLocation(UpdateLocationRequest request, CancellationToken cancellationToken)
    {
        if (RequireUser(out var userId) is { } unauthorized) return unauthorized;

        var result = await _mediator.Send(
            new UpdateProfileLocationCommand(userId, request.PrivacyLevel, request.CountryId, request.RegionId, request.CityId),
            cancellationToken);

        return result.Outcome switch
        {
            UpdateProfileLocationOutcome.Updated => Ok(),
            UpdateProfileLocationOutcome.InvalidLocationReference => BadRequest(
                new { message = "The referenced country, region, or city does not exist." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("places")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddPlace(AddPlaceRequest request, CancellationToken cancellationToken)
    {
        if (RequireUser(out var userId) is { } unauthorized) return unauthorized;

        var result = await _mediator.Send(new AddMyPlaceCommand(userId, request.CityId), cancellationToken);

        return result.Outcome switch
        {
            AddMyPlaceOutcome.Added or AddMyPlaceOutcome.AlreadyFollowing => Ok(),
            AddMyPlaceOutcome.CityNotFound => NotFound(new { message = "City not found." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpDelete("places/{cityId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemovePlace(Guid cityId, CancellationToken cancellationToken)
    {
        if (RequireUser(out var userId) is { } unauthorized) return unauthorized;

        var result = await _mediator.Send(new RemoveMyPlaceCommand(userId, cityId), cancellationToken);

        return result.Outcome switch
        {
            RemoveMyPlaceOutcome.Removed => Ok(),
            RemoveMyPlaceOutcome.NotFollowing => NotFound(new { message = "You are not following this city." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
