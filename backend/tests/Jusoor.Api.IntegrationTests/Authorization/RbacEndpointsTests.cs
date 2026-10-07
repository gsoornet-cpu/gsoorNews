using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Jusoor.Domain.Enums;
using Jusoor.Infrastructure.Identity;
using Jusoor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jusoor.Api.IntegrationTests.Authorization;

/// <summary>
/// Slice 17 — the remaining approved-matrix rows that have endpoints:
/// incoming stories, source ingestion, and newsroom role assignment.
/// Every rule is tested on both the allow and the deny side.
/// </summary>
public class RbacEndpointsTests : IClassFixture<JusoorApiFactory>
{
    private readonly JusoorApiFactory _factory;

    public RbacEndpointsTests(JusoorApiFactory factory) => _factory = factory;

    private sealed record Actor(HttpClient Client, string UserId, string Email);

    private async Task<Actor> NewActorAsync(string? role)
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";
        const string password = "P@ssword1";

        await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, displayName = "Test User" });

        string userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            userId = user!.Id;
            if (role is not null)
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }

        // Log in AFTER the role is assigned: the JWT carries the roles it was issued with.
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());

        return new Actor(client, userId, email);
    }

    private async Task<List<string>> RolesOfAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId);
        return (await userManager.GetRolesAsync(user!)).ToList();
    }

    // ---------------- incoming stories (internal) ----------------

    [Fact]
    public async Task Incoming_stories_should_no_longer_be_public()
    {
        var anonymous = _factory.CreateClient();

        (await anonymous.GetAsync("/api/v1/stories")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync($"/api/v1/stories/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Incoming_stories_should_be_forbidden_for_a_signed_in_user_without_a_newsroom_role()
    {
        var reader = await NewActorAsync(null);

        (await reader.Client.GetAsync("/api/v1/stories")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(NewsroomRole.Reporter)]
    [InlineData(NewsroomRole.Researcher)]
    [InlineData(NewsroomRole.FactChecker)]
    [InlineData(NewsroomRole.SystemAdmin)]
    public async Task Incoming_stories_should_be_readable_by_newsroom_roles(string role)
    {
        var actor = await NewActorAsync(role);

        (await actor.Client.GetAsync("/api/v1/stories")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------- source ingestion ----------------

    [Fact]
    public async Task Ingestion_should_reject_anonymous_callers()
    {
        var anonymous = _factory.CreateClient();

        (await anonymous.PostAsync($"/api/v1/sources/{Guid.NewGuid()}/ingest", null)).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(NewsroomRole.Reporter)]
    [InlineData(NewsroomRole.Researcher)]   // read-only on the source-management row
    [InlineData(NewsroomRole.SeniorEditor)] // read-only on the source-management row
    [InlineData(NewsroomRole.CrisisEditor)]
    public async Task Ingestion_should_be_forbidden_for_roles_without_source_management(string? role)
    {
        var actor = await NewActorAsync(role);

        (await actor.Client.PostAsync($"/api/v1/sources/{Guid.NewGuid()}/ingest", null)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(NewsroomRole.ManagingEditor)]
    [InlineData(NewsroomRole.EditorInChief)]
    [InlineData(NewsroomRole.SystemAdmin)]
    public async Task Ingestion_should_pass_authorization_for_source_managers(string role)
    {
        var actor = await NewActorAsync(role);

        // An unknown source id: an authorized caller reaches the handler and gets
        // its 400; an unauthorized one would have been stopped with 401/403.
        var status = (await actor.Client.PostAsync($"/api/v1/sources/{Guid.NewGuid()}/ingest", null)).StatusCode;

        status.Should().NotBe(HttpStatusCode.Unauthorized).And.NotBe(HttpStatusCode.Forbidden);
    }

    // ---------------- role assignment: who may view ----------------

    [Fact]
    public async Task Role_assignment_endpoints_should_reject_anonymous_callers()
    {
        var anonymous = _factory.CreateClient();

        (await anonymous.GetAsync("/api/v1/admin/users")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/v1/admin/users/x/roles", new { role = "Reporter" })).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(NewsroomRole.Reporter)]
    [InlineData(NewsroomRole.ManagingEditor)]
    [InlineData(NewsroomRole.SeniorEditor)]
    public async Task Listing_users_should_be_forbidden_below_editor_in_chief(string? role)
    {
        var actor = await NewActorAsync(role);

        (await actor.Client.GetAsync("/api/v1/admin/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(NewsroomRole.EditorInChief)]
    [InlineData(NewsroomRole.SystemAdmin)]
    public async Task Listing_users_should_work_for_editor_in_chief_and_system_admin(string role)
    {
        var actor = await NewActorAsync(role);

        var response = await actor.Client.GetAsync($"/api/v1/admin/users?search={Uri.EscapeDataString(actor.Email)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("items").EnumerateArray().Should()
            .Contain(u => u.GetProperty("id").GetString() == actor.UserId);
    }

    [Fact]
    public async Task Listing_users_should_reject_an_oversized_page()
    {
        var admin = await NewActorAsync(NewsroomRole.SystemAdmin);

        (await admin.Client.GetAsync("/api/v1/admin/users?pageSize=1000")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------------- role assignment: who may change ----------------

    [Theory]
    [InlineData(NewsroomRole.EditorInChief)]   // may VIEW but not change
    [InlineData(NewsroomRole.ManagingEditor)]
    [InlineData(NewsroomRole.Reporter)]
    public async Task Changing_roles_should_be_forbidden_for_everyone_but_system_admin(string role)
    {
        var actor = await NewActorAsync(role);
        var target = await NewActorAsync(null);

        (await actor.Client.PostAsJsonAsync($"/api/v1/admin/users/{target.UserId}/roles", new { role = NewsroomRole.SystemAdmin }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await actor.Client.DeleteAsync($"/api/v1/admin/users/{target.UserId}/roles/{NewsroomRole.Reporter}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await RolesOfAsync(target.UserId)).Should().BeEmpty();
    }

    [Fact]
    public async Task System_admin_can_grant_and_revoke_a_role_and_each_change_is_audited()
    {
        var admin = await NewActorAsync(NewsroomRole.SystemAdmin);
        var target = await NewActorAsync(null);

        var grant = await admin.Client.PostAsJsonAsync(
            $"/api/v1/admin/users/{target.UserId}/roles", new { role = NewsroomRole.SeniorEditor });
        grant.StatusCode.Should().Be(HttpStatusCode.OK);
        (await RolesOfAsync(target.UserId)).Should().Contain(NewsroomRole.SeniorEditor);

        var revoke = await admin.Client.DeleteAsync($"/api/v1/admin/users/{target.UserId}/roles/{NewsroomRole.SeniorEditor}");
        revoke.StatusCode.Should().Be(HttpStatusCode.OK);
        (await RolesOfAsync(target.UserId)).Should().BeEmpty();

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var audits = await context.RoleChangeAudits.AsNoTracking()
            .Where(a => a.TargetUserId == target.UserId).OrderBy(a => a.OccurredAtUtc).ToListAsync();
        audits.Select(a => (a.Action, a.ActorUserId, a.Role)).Should().Equal(
            (RoleChangeAction.Granted, admin.UserId, NewsroomRole.SeniorEditor),
            (RoleChangeAction.Revoked, admin.UserId, NewsroomRole.SeniorEditor));
    }

    [Fact]
    public async Task Granting_an_unknown_role_or_to_an_unknown_user_should_fail_cleanly()
    {
        var admin = await NewActorAsync(NewsroomRole.SystemAdmin);
        var target = await NewActorAsync(null);

        (await admin.Client.PostAsJsonAsync($"/api/v1/admin/users/{target.UserId}/roles", new { role = "Superuser" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.Client.PostAsJsonAsync($"/api/v1/admin/users/{Guid.NewGuid()}/roles", new { role = NewsroomRole.Reporter }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await RolesOfAsync(target.UserId)).Should().BeEmpty();
    }

    [Fact]
    public async Task System_admin_cannot_remove_their_own_system_admin_role()
    {
        var admin = await NewActorAsync(NewsroomRole.SystemAdmin);

        var response = await admin.Client.DeleteAsync($"/api/v1/admin/users/{admin.UserId}/roles/{NewsroomRole.SystemAdmin}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await RolesOfAsync(admin.UserId)).Should().Contain(NewsroomRole.SystemAdmin);
    }

    // ---------------- guard rails that must NOT have changed ----------------

    [Fact]
    public async Task System_admin_still_has_no_atmaen_case_access()
    {
        var admin = await NewActorAsync(NewsroomRole.SystemAdmin);

        // Approved matrix: SystemAdmin has no standing Case-content access (break-glass only, deferred).
        (await admin.Client.GetAsync("/api/v1/atmaen/cases/my-queue")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
