namespace Jusoor.Domain.Enums;

/// <summary>
/// Per §19: transient/permanent/validation/security errors must be
/// distinguishable, not collapsed into one generic "failed" bucket — the
/// retry policy (later slice) and alerting both depend on knowing which
/// kind of failure this was.
/// </summary>
public enum SourceFetchOutcome
{
    Success = 1,
    Timeout = 2,               // transient — safe to retry with backoff
    TransientNetworkError = 3, // transient — DNS failure, connection reset, etc.
    HttpError = 4,              // permanent-ish — 4xx/5xx from the source itself
    SecurityRejected = 5,       // permanent — SSRF guard blocked the target; never retry as-is
    ResponseTooLarge = 6,       // permanent — exceeded the configured byte cap
    ParseError = 7              // permanent — fetched fine, content was malformed
}
