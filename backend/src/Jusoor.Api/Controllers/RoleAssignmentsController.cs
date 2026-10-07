using Jusoor.Application.Administration;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Common.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jusoor.Api.Controllers;

/// <summary>
/// Newsroom role assignment. Matrix: EditorInChief may view, SystemAdmin may
/// view and change; everyone else has no access. Every change is audited
/// (RoleChangeAudits) and logged.
/// </summary>
[ApiController]
[Route("api/v1/admin/users")]
public class RoleAssignmentsController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ICurrentUserService _currentUser;

    public RoleAssignmentsController(ISender mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    public record GrantRoleRequest(string Role);

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.CanViewRoleAssignments)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search, [FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ListNewsroomUsersQuery(search, page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize, _currentUser.Roles),
            cancellationToken);

        return result is null ? Forbid() : Ok(result);
    }

    [HttpPost("{userId}/roles")]
    [Authorize(Policy = AuthorizationPolicies.CanManageRoleAssignments)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Grant(string userId, GrantRoleRequest request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } actorId)
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(
            new GrantRoleCommand(userId, request.Role ?? string.Empty, actorId, _currentUser.Roles), cancellationToken);

        return ToActionResult(result);
    }

    [HttpDelete("{userId}/roles/{role}")]
    [Authorize(Policy = AuthorizationPolicies.CanManageRoleAssignments)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Revoke(string userId, string role, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } actorId)
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(new RevokeRoleCommand(userId, role, actorId, _currentUser.Roles), cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(RoleChangeResult result) => result.Outcome switch
    {
        RoleChangeOutcome.Success or RoleChangeOutcome.Unchanged => Ok(result.User),
        RoleChangeOutcome.NotFound => NotFound(new { message = "User not found." }),
        RoleChangeOutcome.InvalidRole => BadRequest(new { message = "Unknown newsroom role." }),
        RoleChangeOutcome.Conflict => Conflict(new { message = result.Reason ?? "The change is not allowed." }),
        RoleChangeOutcome.Forbidden => Forbid(),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };
}
