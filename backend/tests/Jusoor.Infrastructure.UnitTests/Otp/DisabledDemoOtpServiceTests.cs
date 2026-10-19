using FluentAssertions;
using Jusoor.Application.Common.Exceptions;
using Jusoor.Infrastructure.Otp;
using Xunit;

namespace Jusoor.Infrastructure.UnitTests.Otp;

public sealed class DisabledDemoOtpServiceTests
{
    private readonly DisabledDemoOtpService _service = new();

    [Fact]
    public async Task SendVerification_fails_closed_without_generating_an_OTP()
    {
        var act = () => _service.SendVerificationAsync("+201000000000", CancellationToken.None);

        await act.Should().ThrowAsync<OtpProviderException>()
            .WithMessage("Phone verification is disabled in the client preview environment.");
    }

    [Fact]
    public async Task CheckVerification_fails_closed_without_accepting_a_code()
    {
        var act = () => _service.CheckVerificationAsync("+201000000000", "123456", CancellationToken.None);

        await act.Should().ThrowAsync<OtpProviderException>()
            .WithMessage("Phone verification is disabled in the client preview environment.");
    }
}
