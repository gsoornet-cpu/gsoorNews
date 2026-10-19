using Jusoor.Application.Common.Exceptions;
using Jusoor.Application.Common.Interfaces;

namespace Jusoor.Infrastructure.Otp;

/// <summary>
/// Disables applicant phone verification in an isolated client-preview deployment.
/// A preview must never invent or log OTP values; Atmaen endpoints return their
/// normal provider-unavailable response until a real provider is configured.
/// </summary>
public sealed class DisabledDemoOtpService : IOtpService
{
    private static OtpProviderException Disabled() =>
        new("Phone verification is disabled in the client preview environment.");

    public Task<OtpChallengeResult> SendVerificationAsync(
        string phoneNumberE164,
        CancellationToken cancellationToken)
        => throw Disabled();

    public Task<OtpCheckResult> CheckVerificationAsync(
        string phoneNumberE164,
        string code,
        CancellationToken cancellationToken)
        => throw Disabled();
}
