using FluentValidation;
using Jusoor.Application.Common.Exceptions;
using Jusoor.Application.Common.Interfaces;
using MediatR;

namespace Jusoor.Application.Atmaen;

public enum RequestCaseVerificationOutcome
{
    Sent,

    /// <summary>The OTP provider itself failed (network, auth, bad config —
    /// see OtpProviderException) — distinct from the applicant later
    /// entering a wrong code, which is a normal CheckVerificationAsync
    /// result, not a provider failure.</summary>
    ProviderUnavailable
}

public sealed record RequestCaseVerificationResult(bool Succeeded, RequestCaseVerificationOutcome Outcome);

/// <summary>
/// Spec §10 step 5, first half: send the OTP. Deliberately returns no
/// provider-specific detail (no challenge ID, no "check your Twilio app"
/// wording) — Application/Api code stays unaware of which provider is
/// behind IOtpService, same as IOtpService's own design intent.
/// </summary>
public sealed record RequestCaseVerificationCommand(string ApplicantPhoneE164) : IRequest<RequestCaseVerificationResult>;

public class RequestCaseVerificationCommandValidator : AbstractValidator<RequestCaseVerificationCommand>
{
    public RequestCaseVerificationCommandValidator()
    {
        // A permissive but real E.164 check (leading +, 8–15 digits total,
        // no leading 0 after the +) — loose enough not to reject legitimate
        // international numbers, strict enough to catch obviously malformed
        // input before it reaches the OTP provider (and its per-request
        // cost) at all.
        RuleFor(x => x.ApplicantPhoneE164)
            .NotEmpty()
            .Matches(@"^\+[1-9]\d{7,14}$")
            .WithMessage("Phone number must be in E.164 format, e.g. +201001234567.");
    }
}

public class RequestCaseVerificationCommandHandler : IRequestHandler<RequestCaseVerificationCommand, RequestCaseVerificationResult>
{
    private readonly IOtpService _otpService;

    public RequestCaseVerificationCommandHandler(IOtpService otpService)
    {
        _otpService = otpService;
    }

    public async Task<RequestCaseVerificationResult> Handle(RequestCaseVerificationCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _otpService.SendVerificationAsync(request.ApplicantPhoneE164, cancellationToken);
            return new RequestCaseVerificationResult(true, RequestCaseVerificationOutcome.Sent);
        }
        catch (OtpProviderException)
        {
            // Not rethrown/logged as an error here — the provider being
            // temporarily unavailable is an expected, recoverable condition
            // the caller should get a clear signal about (see
            // AtmaenController's 503 mapping), same "expected condition,
            // not a scary error" reasoning ResolveReviewCommand already
            // applies to its own conflict case.
            return new RequestCaseVerificationResult(false, RequestCaseVerificationOutcome.ProviderUnavailable);
        }
    }
}
