namespace Jusoor.Application.Common.Exceptions;

/// <summary>
/// Thrown when the configured OTP provider (Infrastructure's <c>IOtpService</c>
/// implementation) fails in a way the caller cannot recover from — e.g. the
/// provider is unreachable, rejects the request outright, or configuration is
/// invalid. Deliberately provider-agnostic: it never wraps or exposes a
/// Twilio-specific (or any other vendor-specific) exception type, so Application
/// code stays unaware of which provider is behind <c>IOtpService</c>.
///
/// No HTTP-status mapping exists for this yet in
/// <c>ExceptionHandlingMiddleware</c> — it currently falls through to that
/// middleware's generic 500 handler. The Atmaen Case submission endpoint (not
/// yet built) is the right place to decide the actual applicant-facing
/// behavior (e.g. "try again" vs. a specific retry-after response), once that
/// endpoint exists — left as a genuinely open question rather than guessed at
/// here.
/// </summary>
public class OtpProviderException : Exception
{
    public OtpProviderException(string message) : base(message)
    {
    }

    public OtpProviderException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
