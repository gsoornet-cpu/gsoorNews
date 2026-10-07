using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Jusoor.Domain.Enums;
using Jusoor.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jusoor.Api.IntegrationTests.Atmaen;

/// <summary>
/// Covers what's reachable end-to-end without a live Twilio account: request
/// validation on the anonymous submission endpoint (which runs in the
/// MediatR pipeline before IOtpService is ever called), and every
/// authorization/assignment-scoping path on the CrisisEditor-facing
/// endpoints (all of which return before needing a real Case with a real
/// verified OTP). NOT covered here: the actual happy path of submitting and
/// having a case created — that needs a real Twilio account this sandbox
/// doesn't have, and remains a genuinely open verification gap tracked in
/// the dashboard, not silently assumed to work.
/// </summary>
public class AtmaenEndpointsTests : IClassFixture<JusoorApiFactory>
{
    private readonly JusoorApiFactory _factory;
    private readonly HttpClient _client;

    public AtmaenEndpointsTests(JusoorApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAndLoginAsync(string? role)
    {
        var email = $"{Guid.NewGuid()}@example.com";
        const string password = "P@ssword1";

        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, displayName = "Test CrisisEditor" });

        if (role is not null)
        {
            using var scope = _factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            await userManager.AddToRoleAsync(user!, role);
        }

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    [Fact]
    public async Task SubmitCase_with_a_missing_subject_name_should_be_rejected_before_ever_reaching_the_OTP_provider()
    {
        // No SubjectName at all — FluentValidation's NotEmpty rule fails in
        // the MediatR pipeline, before SubmitCaseCommandHandler runs, so
        // this exercises real request routing/validation without needing
        // Twilio to accept anything.
        var response = await _client.PostAsJsonAsync("/api/v1/atmaen/cases", new
        {
            countryId = Guid.NewGuid(),
            cityId = Guid.NewGuid(),
            subjectName = "",
            concernDescription = "hasn't answered calls in three days",
            applicantPhoneE164 = "+201001234567",
            otpCode = "123456"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitCase_with_a_malformed_phone_number_should_be_rejected_by_validation()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/atmaen/cases", new
        {
            countryId = Guid.NewGuid(),
            cityId = Guid.NewGuid(),
            subjectName = "Ahmed Mostafa",
            concernDescription = "hasn't answered calls in three days",
            applicantPhoneE164 = "not-a-phone-number",
            otpCode = "123456"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMyQueue_without_a_token_should_be_unauthorized()
    {
        var response = await _client.GetAsync("/api/v1/atmaen/cases/my-queue");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyQueue_with_a_token_but_no_CrisisEditor_role_should_be_forbidden()
    {
        var token = await RegisterAndLoginAsync(role: null);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/v1/atmaen/cases/my-queue");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetMyQueue_for_a_CrisisEditor_with_no_assigned_cases_should_return_an_empty_page()
    {
        var token = await RegisterAndLoginAsync(NewsroomRole.CrisisEditor);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/v1/atmaen/cases/my-queue");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task GetCase_for_an_unknown_id_should_be_not_found()
    {
        var token = await RegisterAndLoginAsync(NewsroomRole.CrisisEditor);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync($"/api/v1/atmaen/cases/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateStatus_for_an_unknown_id_should_be_not_found()
    {
        var token = await RegisterAndLoginAsync(NewsroomRole.CrisisEditor);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/atmaen/cases/{Guid.NewGuid()}/status",
            new { newStatus = CaseStatus.VerificationInProgress });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
