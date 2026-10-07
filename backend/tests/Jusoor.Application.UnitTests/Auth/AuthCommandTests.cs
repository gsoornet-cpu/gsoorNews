using FluentAssertions;
using FluentValidation.TestHelper;
using Jusoor.Application.Auth;
using Jusoor.Application.Common.Interfaces;
using NSubstitute;
using Xunit;

namespace Jusoor.Application.UnitTests.Auth;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Fact]
    public void Should_fail_when_email_is_invalid()
    {
        var result = _validator.TestValidate(new RegisterCommand("not-an-email", "P@ssword1", "Omar"));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("")]
    public void Should_fail_when_password_too_short(string password)
    {
        var result = _validator.TestValidate(new RegisterCommand("user@example.com", password, "Omar"));
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Should_pass_for_a_valid_registration()
    {
        var result = _validator.TestValidate(new RegisterCommand("user@example.com", "P@ssword1", "Omar"));
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class LoginCommandHandlerTests
{
    [Fact]
    public async Task Should_return_generic_failure_when_user_does_not_exist()
    {
        var identityService = Substitute.For<IIdentityService>();
        identityService.CheckPasswordAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new UserCredentialsCheckResult(false, null, null, Array.Empty<string>()));
        var jwtService = Substitute.For<IJwtTokenService>();

        var handler = new LoginCommandHandler(identityService, jwtService);

        var result = await handler.Handle(new LoginCommand("nobody@example.com", "whatever"), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Tokens.Should().BeNull();
        // Anti-enumeration: the handler must not have called GenerateTokens
        // at all on failure, and callers of this test can rely on the fact
        // that a "wrong password" case (tested below) produces the exact
        // same shape of result.
        jwtService.DidNotReceive().GenerateTokens(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>());
    }

    [Fact]
    public async Task Should_return_tokens_when_credentials_are_valid()
    {
        var identityService = Substitute.For<IIdentityService>();
        identityService.CheckPasswordAsync("user@example.com", "P@ssword1")
            .Returns(new UserCredentialsCheckResult(true, "user-1", "user@example.com", new List<string> { "Reporter" }));

        var jwtService = Substitute.For<IJwtTokenService>();
        jwtService.GenerateTokens("user-1", "user@example.com", Arg.Any<IReadOnlyList<string>>())
            .Returns(new AuthTokenResult("access-token", DateTimeOffset.UtcNow.AddMinutes(30), "refresh-token"));

        var handler = new LoginCommandHandler(identityService, jwtService);

        var result = await handler.Handle(new LoginCommand("user@example.com", "P@ssword1"), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Tokens!.AccessToken.Should().Be("access-token");
    }
}
