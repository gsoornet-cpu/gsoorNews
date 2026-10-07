using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Jusoor.Api.IntegrationTests.Auth;

public class AuthEndpointsTests : IClassFixture<JusoorApiFactory>
{
    private readonly HttpClient _client;

    public AuthEndpointsTests(JusoorApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_then_login_should_issue_a_real_jwt()
    {
        var email = $"{Guid.NewGuid()}@example.com";

        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "P@ssword1",
            displayName = "Test User"
        });

        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "P@ssword1"
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_with_wrong_password_and_login_with_unknown_user_should_return_identical_shapes()
    {
        // Anti-enumeration regression guard: both cases must be
        // indistinguishable to a caller (same status code, same body shape).
        var email = $"{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "P@ssword1", displayName = "Test User" });

        var wrongPassword = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "WrongPass1" });
        var unknownUser = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = $"{Guid.NewGuid()}@example.com", password = "WrongPass1" });

        wrongPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknownUser.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var wrongPasswordBody = await wrongPassword.Content.ReadAsStringAsync();
        var unknownUserBody = await unknownUser.Content.ReadAsStringAsync();
        wrongPasswordBody.Should().Be(unknownUserBody);
    }
}
