namespace Jusoor.Infrastructure.Otp;

/// <summary>
/// Twilio Verify V2 credentials. Every property here is a secret — unlike
/// <c>JwtOptions</c> (which has non-secret fields like Issuer/Audience that
/// safely live in appsettings.json), there is no non-secret Twilio
/// configuration worth committing, so nothing for this section appears in
/// appsettings.json at all. Real values come from Key Vault in
/// staging/production and from user-secrets/.env locally — same rule
/// JwtOptions.SigningKey already follows, applied here to all three fields.
/// Twilio account and its costs are owned by the project (Phase 2 decision 1,
/// approved) — this class only reads whatever credentials it's given.
/// </summary>
public class TwilioOptions
{
    public const string SectionName = "Twilio";

    public required string AccountSid { get; set; }
    public required string AuthToken { get; set; }

    /// <summary>The Verify Service SID (starts with "VA..."), created once in the
    /// Twilio console for this project — not a per-verification value.</summary>
    public required string VerifyServiceSid { get; set; }
}
