using FluentAssertions;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Infrastructure.Otp;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Jusoor.Infrastructure.UnitTests.Otp;

/// <summary>
/// Covers what is genuinely unit-testable without a live Twilio account or
/// network access: the pure status-mapping function, and this class's
/// fail-fast behavior when the Twilio configuration section is missing.
///
/// NOT covered here, and NOT verified by this session: the actual
/// SendVerificationAsync/CheckVerificationAsync calls against Twilio's real
/// API (including via Twilio's documented "magic number" test credentials,
/// which still require live network access to api.twilio.com — unavailable
/// in this sandbox, same restriction already noted for api.nuget.org on
/// every prior slice). That path needs a developer run with real Twilio
/// credentials before it can be called verified.
/// </summary>
public class TwilioOtpServiceTests
{
    [Theory]
    [InlineData("approved", OtpVerificationStatus.Approved)]
    [InlineData("pending", OtpVerificationStatus.Pending)]
    [InlineData("expired", OtpVerificationStatus.Expired)]
    [InlineData("canceled", OtpVerificationStatus.Denied)]
    [InlineData("max_attempts_reached", OtpVerificationStatus.Denied)]
    [InlineData("deleted", OtpVerificationStatus.Denied)]
    [InlineData("failed", OtpVerificationStatus.Denied)]
    public void MapStatus_maps_every_documented_Twilio_status(string twilioStatus, OtpVerificationStatus expected)
    {
        TwilioOtpService.MapStatus(twilioStatus).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("some_future_status_twilio_has_not_documented_yet")]
    public void MapStatus_treats_unknown_or_missing_status_as_Denied_never_Approved(string? unknownStatus)
    {
        // An unrecognized status must never be silently treated as a pass —
        // this is the one property of this mapping worth pinning down with a
        // test independent of Twilio ever changing its status vocabulary.
        TwilioOtpService.MapStatus(unknownStatus).Should().Be(OtpVerificationStatus.Denied);
    }

    [Fact]
    public void Constructor_throws_when_Twilio_configuration_section_is_missing()
    {
        var configuration = new ConfigurationBuilder().Build(); // no "Twilio" section at all

        var act = () => new TwilioOtpService(configuration);

        // Same fail-fast contract as JwtTokenService: refuse to start with no
        // OTP provider configured rather than run with a default/guessable one.
        act.Should().Throw<InvalidOperationException>();
    }
}
