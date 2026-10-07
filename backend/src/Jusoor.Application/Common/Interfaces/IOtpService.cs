namespace Jusoor.Application.Common.Interfaces;

/// <summary>
/// Outcome of a completed verification check, deliberately narrower than
/// whatever status vocabulary the underlying provider uses. New provider
/// statuses map onto one of these four rather than leaking a provider-specific
/// string into Application code — see the provider implementation's own
/// mapping function for the exhaustive provider-status -> this-enum table.
/// </summary>
public enum OtpVerificationStatus
{
    /// <summary>Code sent, not yet checked (or check not yet resolved by the provider).</summary>
    Pending,

    /// <summary>Code matched. Application code may proceed with the identity-verified path.</summary>
    Approved,

    /// <summary>Code did not match, too many attempts were made, or the provider otherwise
    /// refused the check. Treated the same as an incorrect code — callers must not
    /// distinguish "wrong code" from "provider refused" in a way that reveals which
    /// occurred, per spec §13's anti-impersonation intent.</summary>
    Denied,

    /// <summary>The verification window elapsed before a check was made.</summary>
    Expired
}

/// <summary>Correlation handle for a sent verification. <c>ChallengeId</c> is an opaque
/// provider reference for logging/support purposes only — it is never the OTP code
/// itself, is never shown to the applicant, and (for providers that verify by
/// phone number rather than by challenge, such as Twilio Verify) is not required to
/// perform the check.</summary>
public record OtpChallengeResult(string ChallengeId, OtpVerificationStatus Status);

public record OtpCheckResult(OtpVerificationStatus Status);

/// <summary>
/// Sends and checks a one-time-passcode challenge to verify that whoever is
/// submitting an Atmaen case (spec §10, step 5) actually controls the phone
/// number they gave. This interface is provider-agnostic by design (Phase 2
/// decision 1, approved): the concrete adapter lives in Infrastructure and can
/// be replaced without any change to Application/Domain code that depends on
/// this interface.
///
/// Callers must NOT implement their own rate-limiting/lockout/retry logic on
/// top of this interface as a substitute for what the provider already does —
/// per the approved decision, the provider's own verification lifecycle and
/// fraud-protection capabilities (e.g. Twilio Verify's max-attempts lockout
/// and Fraud Guard) are the intended anti-impersonation mechanism, not a
/// parallel one built here.
/// </summary>
public interface IOtpService
{
    /// <param name="phoneNumberE164">The applicant's phone number in E.164 format
    /// (e.g. "+201001234567"). Validating/normalizing the format is the caller's
    /// responsibility — this interface does not attempt to guess a country code.</param>
    Task<OtpChallengeResult> SendVerificationAsync(string phoneNumberE164, CancellationToken cancellationToken);

    /// <param name="phoneNumberE164">Must be the same number passed to
    /// <see cref="SendVerificationAsync"/> — most providers (Twilio Verify included)
    /// key the pending verification by phone number, not by <c>ChallengeId</c>.</param>
    /// <param name="code">The code the applicant entered. Never logged, never persisted —
    /// only the resulting <see cref="OtpVerificationStatus"/> is meaningful to callers.</param>
    Task<OtpCheckResult> CheckVerificationAsync(string phoneNumberE164, string code, CancellationToken cancellationToken);
}
