using Jusoor.Application.Common.Exceptions;
using Jusoor.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Twilio.Clients;
using Twilio.Exceptions;
using Twilio.Rest.Verify.V2.Service;

namespace Jusoor.Infrastructure.Otp;

/// <summary>
/// Twilio Verify V2 implementation of <see cref="IOtpService"/> — Phase 2
/// decision 1 (approved): "Use Twilio Verify for Atmaen applicant phone
/// verification... follow Twilio Verify's verification lifecycle and
/// rate-limiting/fraud-protection capabilities rather than implementing a
/// parallel OTP mechanism."
///
/// Deliberately does NOT call the static <c>Twilio.TwilioClient.Init(...)</c>
/// (which mutates process-wide global state — the exact pattern this
/// project's Slice 11 fix just finished removing reliance on for Hangfire).
/// Instead builds one <see cref="TwilioRestClient"/> per service instance and
/// passes it explicitly to every call, so this class has no shared mutable
/// static state and is safe to construct more than once in the same process
/// (e.g. under a test host).
///
/// This class holds no OTP code, ever — Twilio Verify never returns the code
/// to the caller; only a check result. There is nothing here to persist.
/// </summary>
public class TwilioOtpService : IOtpService
{
    private readonly TwilioOptions _options;
    private readonly TwilioRestClient _client;

    public TwilioOtpService(IConfiguration configuration)
    {
        _options = configuration.GetSection(TwilioOptions.SectionName).Get<TwilioOptions>()
            ?? throw new InvalidOperationException(
                "Twilio configuration section is missing. Refusing to start with no OTP provider configured " +
                "rather than falling back to a mechanism of our own — see the approved Phase 2 decision.");

        _client = new TwilioRestClient(_options.AccountSid, _options.AuthToken);
    }

    public async Task<OtpChallengeResult> SendVerificationAsync(string phoneNumberE164, CancellationToken cancellationToken)
    {
        try
        {
            var verification = await VerificationResource.CreateAsync(
                to: phoneNumberE164,
                channel: "sms",
                pathServiceSid: _options.VerifyServiceSid,
                client: _client);

            return new OtpChallengeResult(verification.Sid, MapStatus(verification.Status?.ToString()));
        }
        catch (ApiException ex)
        {
            // Twilio's own message can include the phone number we sent — do not
            // let it reach logs/callers verbatim in a way that would put a raw
            // phone number in application logs beyond what's already necessary.
            throw new OtpProviderException("The OTP provider rejected the verification request.", ex);
        }
    }

    public async Task<OtpCheckResult> CheckVerificationAsync(string phoneNumberE164, string code, CancellationToken cancellationToken)
    {
        try
        {
            var check = await VerificationCheckResource.CreateAsync(
                to: phoneNumberE164,
                code: code,
                pathServiceSid: _options.VerifyServiceSid,
                client: _client);

            return new OtpCheckResult(MapStatus(check.Status?.ToString()));
        }
        catch (ApiException ex)
        {
            // A wrong/expired code is a normal *result* (see MapStatus) and never
            // throws — this catch is only for the provider call itself failing
            // (network, auth, bad service SID, etc.), which is a different kind
            // of failure than "the applicant typed the wrong code."
            throw new OtpProviderException("The OTP provider rejected the verification check.", ex);
        }
    }

    /// <summary>
    /// Maps Twilio Verify's provider-specific status vocabulary onto
    /// <see cref="OtpVerificationStatus"/>. Public and pure (no I/O) so it can be
    /// unit-tested directly without a live Twilio account — see
    /// TwilioOtpServiceTests. Every Twilio Verify status documented for the
    /// Verification and VerificationCheck resources is listed explicitly; an
    /// unrecognized value maps to <see cref="OtpVerificationStatus.Denied"/> rather
    /// than <see cref="OtpVerificationStatus.Approved"/> — an unknown status must
    /// never be treated as a pass.
    /// </summary>
    public static OtpVerificationStatus MapStatus(string? twilioStatus) => twilioStatus switch
    {
        "approved" => OtpVerificationStatus.Approved,
        "pending" => OtpVerificationStatus.Pending,
        "expired" => OtpVerificationStatus.Expired,
        "canceled" => OtpVerificationStatus.Denied,
        "max_attempts_reached" => OtpVerificationStatus.Denied,
        "deleted" => OtpVerificationStatus.Denied,
        "failed" => OtpVerificationStatus.Denied,
        _ => OtpVerificationStatus.Denied
    };
}
