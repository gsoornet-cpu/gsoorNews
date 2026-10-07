using Jusoor.Application.Atmaen;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Common.Security;
using Jusoor.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jusoor.Api.Controllers;

/// <summary>
/// Atmaen ("اطمّن") — spec §10. Two very different audiences share this
/// controller, so [Authorize] is applied per action, not per class:
///
/// - The applicant-facing pair (request a verification code, submit a case)
///   is [AllowAnonymous]. ASSUMPTION, NOT AN APPROVED DECISION: whether
///   submitting an Atmaen case should require a Jusoor account is not one of
///   the six approved Phase 2 decisions. Anonymous, phone-OTP-verified
///   submission is used here because Case.CreatedByUserId is already nullable
///   (inherited from AuditableEntity) and because requiring account signup
///   from someone reporting a person in danger is a real usability/safety
///   cost. Adding [Authorize] later is a one-line change if the answer is
///   different — but it needs confirming before this ships.
///
/// - The CrisisEditor-facing trio (my queue, one case, update status) is
///   gated by CanAccessAtmaenCaseQueue (CrisisEditor only — not SystemAdmin,
///   per the approved break-glass-only rule) AND each handler additionally
///   restricts to Cases assigned to the authenticated user's own id.
///
/// No rate limiting on the anonymous endpoints yet — the approved decision
/// is to rely on Twilio Verify's own max-attempts lockout and Fraud Guard
/// rather than build a parallel mechanism; an app-level throttle on the
/// send-code endpoint (each call costs money) is an open follow-up.
/// </summary>
[ApiController]
[Route("api/v1/atmaen")]
public class AtmaenController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ICurrentUserService _currentUser;

    public AtmaenController(ISender mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    public record RequestVerificationRequest(string ApplicantPhoneE164);

    public record SubmitCaseRequest(
        Guid CountryId,
        Guid CityId,
        string SubjectName,
        string? SubjectContactPhone,
        string ConcernDescription,
        string ApplicantPhoneE164,
        string OtpCode);

    public record UpdateCaseStatusRequest(CaseStatus NewStatus);

    // ---- Applicant-facing (anonymous) ----

    [HttpPost("verification-requests")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RequestVerification(RequestVerificationRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RequestCaseVerificationCommand(request.ApplicantPhoneE164), cancellationToken);

        return result.Outcome switch
        {
            RequestCaseVerificationOutcome.Sent => Accepted(new { message = "A verification code has been sent." }),
            RequestCaseVerificationOutcome.ProviderUnavailable => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "We couldn't send a verification code right now. Please try again shortly." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("cases")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SubmitCase(SubmitCaseRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new SubmitCaseCommand(
                request.CountryId,
                request.CityId,
                request.SubjectName,
                request.SubjectContactPhone,
                request.ConcernDescription,
                request.ApplicantPhoneE164,
                request.OtpCode),
            cancellationToken);

        return result.Outcome switch
        {
            SubmitCaseOutcome.Created => StatusCode(StatusCodes.Status201Created, new { caseId = result.CaseId }),

            // Wrong, expired, and refused codes all get this one message —
            // see OtpVerificationStatus: callers must not reveal which it
            // was (spec §13's anti-impersonation intent).
            SubmitCaseOutcome.OtpNotVerified => BadRequest(new { message = "The verification code was not accepted." }),

            SubmitCaseOutcome.InvalidLocation => BadRequest(new { message = "The selected country or city is not valid." }),

            // The newsroom hasn't staffed the CrisisEditor role: an internal
            // operational gap. The applicant gets a generic "try again"
            // message, not a description of our staffing.
            SubmitCaseOutcome.NoCrisisEditorAvailable => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "We can't accept new cases right now. Please try again shortly." }),

            SubmitCaseOutcome.ProviderUnavailable => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "We couldn't verify your code right now. Please try again shortly." }),

            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    // ---- CrisisEditor-facing (assigned Cases only) ----

    [HttpGet("cases/my-queue")]
    [Authorize(Policy = AuthorizationPolicies.CanAccessAtmaenCaseQueue)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyQueue([FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken)
    {
        // Same defense-in-depth null-check as ReviewController: [Authorize]
        // already guarantees a UserId, this is not an expected path.
        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(
            new GetMyAssignedCasesQuery(userId, page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("cases/{caseId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CanAccessAtmaenCaseQueue)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCase(Guid caseId, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(new GetAssignedCaseByIdQuery(caseId, userId), cancellationToken);

        // Same 404 whether the Case doesn't exist or belongs to another
        // CrisisEditor — deliberately indistinguishable (see the query).
        return result is null ? NotFound(new { message = "Case not found." }) : Ok(result);
    }

    [HttpPost("cases/{caseId:guid}/status")]
    [Authorize(Policy = AuthorizationPolicies.CanAccessAtmaenCaseQueue)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(Guid caseId, UpdateCaseStatusRequest request, CancellationToken cancellationToken)
    {
        // CrisisEditorUserId comes from the authenticated principal, never
        // the request body — the assignment check in the handler compares
        // against exactly this value.
        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(new UpdateCaseStatusCommand(caseId, request.NewStatus, userId), cancellationToken);

        return result.Outcome switch
        {
            UpdateCaseStatusOutcome.Updated => Ok(new { status = result.NewStatus }),

            // Internally distinct, externally identical: a CrisisEditor
            // cannot use this endpoint to learn which Case ids exist
            // outside their own assignments.
            UpdateCaseStatusOutcome.CaseNotFound or UpdateCaseStatusOutcome.NotAssignedToYou
                => NotFound(new { message = "Case not found." }),

            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
