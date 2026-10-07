using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Jusoor.Infrastructure.Identity;
using Jusoor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jusoor.Api.IntegrationTests.Review;

/// <summary>
/// End-to-end coverage for the human review queue — including the first
/// real RBAC authorization test in this codebase (every endpoint before
/// this slice was either fully public or just "any authenticated user").
/// Verifying [Authorize(Policy = ...)] actually rejects the right requests
/// against a real running pipeline is exactly the kind of thing a unit
/// test on the handler alone cannot prove.
/// </summary>
public class ReviewEndpointsTests : IClassFixture<JusoorApiFactory>
{
    private readonly JusoorApiFactory _factory;
    private readonly HttpClient _client;

    public ReviewEndpointsTests(JusoorApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAndLoginAsync(string role)
    {
        var email = $"{Guid.NewGuid()}@example.com";
        const string password = "P@ssword1";

        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, displayName = "Test Editor" });

        // Roles are never self-assignable through the public API (there is
        // no such endpoint, deliberately) — granting one directly through
        // Identity here is the realistic equivalent of "an admin already
        // set this account up," not a shortcut around real authorization.
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

    private async Task<Guid> SeedStoryAwaitingReviewAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var source = new Source { Name = "Integration Test Source", Layer = SourceLayer.ProfessionalMedia, FeedUrl = "https://example.com/feed.xml" };
        var now = DateTimeOffset.UtcNow;
        var story = Story.Create("Integration test story", now);
        story.ApplyRelevanceVerdict(isRelevant: true, confidenceScore: 0.4m, reviewThreshold: 0.6m, now);
        var article = Article.Create(source.Id, Guid.NewGuid().ToString(), $"https://example.com/{Guid.NewGuid()}",
            story.CanonicalTitle, Guid.NewGuid().ToString("N").PadRight(64, 'a')[..64], now, now, "content");
        article.AssignToStory(story, now);

        context.Sources.Add(source);
        context.Stories.Add(story);
        await context.SaveChangesAsync();

        return story.Id;
    }

    [Fact]
    public async Task GetQueue_without_a_token_should_be_unauthorized()
    {
        var response = await _client.GetAsync("/api/v1/review/queue");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetQueue_with_a_token_but_no_newsroom_role_should_be_forbidden()
    {
        var token = await RegisterAndLoginAsync(role: null!);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/v1/review/queue");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Full_review_workflow_should_work_end_to_end_for_an_authorized_editor()
    {
        var storyId = await SeedStoryAwaitingReviewAsync();
        var token = await RegisterAndLoginAsync(NewsroomRole.SeniorEditor);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var queueResponse = await _client.GetAsync("/api/v1/review/queue");
        queueResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var queueBody = await queueResponse.Content.ReadFromJsonAsync<JsonElement>();
        queueBody.GetProperty("items").EnumerateArray().Should().Contain(
            item => item.GetProperty("storyId").GetGuid() == storyId);

        var detailResponse = await _client.GetAsync($"/api/v1/review/queue/{storyId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var resolveResponse = await _client.PostAsJsonAsync($"/api/v1/review/queue/{storyId}/resolve", new
        {
            isRelevant = true,
            reasoning = "Clearly about an Egyptian expatriate abroad, confirmed via consulate mention"
        });
        resolveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var story = await context.Stories.FirstAsync(s => s.Id == storyId);
        story.Status.Should().Be(StoryStatus.Relevant);
        context.HumanReviewDecisions.Should().ContainSingle(d => d.StoryId == storyId && d.ReviewedByUserId != null);
    }

    [Fact]
    public async Task ResolveQueueItem_should_return_conflict_when_story_is_no_longer_awaiting_review()
    {
        var storyId = await SeedStoryAwaitingReviewAsync();
        var token = await RegisterAndLoginAsync(NewsroomRole.EditorInChief);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var first = await _client.PostAsJsonAsync($"/api/v1/review/queue/{storyId}/resolve",
            new { isRelevant = true, reasoning = "First reviewer's reasoning, clearly explained here" });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await _client.PostAsJsonAsync($"/api/v1/review/queue/{storyId}/resolve",
            new { isRelevant = false, reasoning = "Second reviewer arriving too late, explained here" });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ResolveQueueItem_with_insufficient_reasoning_should_return_bad_request()
    {
        var storyId = await SeedStoryAwaitingReviewAsync();
        var token = await RegisterAndLoginAsync(NewsroomRole.SeniorEditor);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync($"/api/v1/review/queue/{storyId}/resolve",
            new { isRelevant = true, reasoning = "ok" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
