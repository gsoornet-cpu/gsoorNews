using FluentAssertions;
using Jusoor.Application.Administration;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.UnitTests.TestSupport;
using Jusoor.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Jusoor.Application.UnitTests.Administration;

public class RoleAdministrationTests
{
    private static readonly string[] Admin = { NewsroomRole.SystemAdmin };
    private static readonly string[] Chief = { NewsroomRole.EditorInChief };
    private static readonly string[] Managing = { NewsroomRole.ManagingEditor };

    private static IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));
        return clock;
    }

    private static NewsroomUserInfo User(string id, params string[] roles) => new(id, $"{id}@x.com", id, roles);

    private static IIdentityService Identity(NewsroomUserInfo? target, params string[] adminIds)
    {
        var identity = Substitute.For<IIdentityService>();
        identity.FindUserAsync(Arg.Any<string>()).Returns(target);
        identity.AddToRoleAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        identity.RemoveFromRoleAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        identity.GetUserIdsInRoleAsync(NewsroomRole.SystemAdmin).Returns(adminIds);
        return identity;
    }

    private static GrantRoleCommandHandler GrantHandler(IIdentityService id, TestApplicationDbContext ctx)
        => new(id, ctx, Clock(), NullLogger<GrantRoleCommandHandler>.Instance);

    private static RevokeRoleCommandHandler RevokeHandler(IIdentityService id, TestApplicationDbContext ctx)
        => new(id, ctx, Clock(), NullLogger<RevokeRoleCommandHandler>.Instance);

    // ---------- access rules ----------

    [Fact]
    public void Matrix_rules_view_is_chief_and_admin_change_is_admin_only()
    {
        RoleAdministrationAccess.CanView(Admin).Should().BeTrue();
        RoleAdministrationAccess.CanView(Chief).Should().BeTrue();
        RoleAdministrationAccess.CanView(Managing).Should().BeFalse();
        RoleAdministrationAccess.CanChange(Admin).Should().BeTrue();
        RoleAdministrationAccess.CanChange(Chief).Should().BeFalse();
        RoleAdministrationAccess.CanChange(Managing).Should().BeFalse();
        RoleAdministrationAccess.CanChange(Array.Empty<string>()).Should().BeFalse();
    }

    // ---------- grant ----------

    [Fact]
    public async Task Grant_by_system_admin_should_assign_the_role_and_write_one_audit_row()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var identity = Identity(User("target"));

        var result = await GrantHandler(identity, ctx).Handle(
            new GrantRoleCommand("target", NewsroomRole.Reporter, "admin-1", Admin), default);

        result.Outcome.Should().Be(RoleChangeOutcome.Success);
        await identity.Received(1).AddToRoleAsync("target", NewsroomRole.Reporter);
        var audit = await ctx.RoleChangeAudits.AsNoTracking().SingleAsync();
        audit.ActorUserId.Should().Be("admin-1");
        audit.TargetUserId.Should().Be("target");
        audit.Role.Should().Be(NewsroomRole.Reporter);
        audit.Action.Should().Be(RoleChangeAction.Granted);
    }

    [Theory]
    [InlineData(NewsroomRole.EditorInChief)]
    [InlineData(NewsroomRole.ManagingEditor)]
    [InlineData(NewsroomRole.SeniorEditor)]
    [InlineData(NewsroomRole.Reporter)]
    public async Task Grant_by_anyone_but_system_admin_should_be_forbidden_and_change_nothing(string actorRole)
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var identity = Identity(User("target"));

        var result = await GrantHandler(identity, ctx).Handle(
            new GrantRoleCommand("target", NewsroomRole.SystemAdmin, "actor", new[] { actorRole }), default);

        result.Outcome.Should().Be(RoleChangeOutcome.Forbidden);
        await identity.DidNotReceiveWithAnyArgs().AddToRoleAsync(default!, default!);
        (await ctx.RoleChangeAudits.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("Superuser")]
    [InlineData("")]
    [InlineData("systemadmin ")]
    public async Task Grant_should_reject_a_role_that_is_not_one_of_the_eleven_newsroom_roles(string role)
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var identity = Identity(User("target"));

        var result = await GrantHandler(identity, ctx).Handle(new GrantRoleCommand("target", role, "admin-1", Admin), default);

        result.Outcome.Should().Be(RoleChangeOutcome.InvalidRole);
        await identity.DidNotReceiveWithAnyArgs().AddToRoleAsync(default!, default!);
    }

    [Fact]
    public async Task Grant_to_an_unknown_user_should_be_not_found()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();

        var result = await GrantHandler(Identity(null), ctx).Handle(
            new GrantRoleCommand("ghost", NewsroomRole.Reporter, "admin-1", Admin), default);

        result.Outcome.Should().Be(RoleChangeOutcome.NotFound);
    }

    [Fact]
    public async Task Grant_of_a_role_the_user_already_has_should_succeed_without_a_duplicate_audit_row()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var identity = Identity(User("target", NewsroomRole.Reporter));

        var result = await GrantHandler(identity, ctx).Handle(
            new GrantRoleCommand("target", NewsroomRole.Reporter, "admin-1", Admin), default);

        result.Outcome.Should().Be(RoleChangeOutcome.Unchanged);
        result.Succeeded.Should().BeTrue();
        await identity.DidNotReceiveWithAnyArgs().AddToRoleAsync(default!, default!);
        (await ctx.RoleChangeAudits.CountAsync()).Should().Be(0);
    }

    // ---------- revoke ----------

    [Fact]
    public async Task Revoke_by_system_admin_should_remove_the_role_and_audit_it()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var identity = Identity(User("target", NewsroomRole.SeniorEditor));

        var result = await RevokeHandler(identity, ctx).Handle(
            new RevokeRoleCommand("target", NewsroomRole.SeniorEditor, "admin-1", Admin), default);

        result.Outcome.Should().Be(RoleChangeOutcome.Success);
        (await ctx.RoleChangeAudits.AsNoTracking().SingleAsync()).Action.Should().Be(RoleChangeAction.Revoked);
    }

    [Fact]
    public async Task Revoke_by_editor_in_chief_should_be_forbidden()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var identity = Identity(User("target", NewsroomRole.SeniorEditor));

        var result = await RevokeHandler(identity, ctx).Handle(
            new RevokeRoleCommand("target", NewsroomRole.SeniorEditor, "chief-1", Chief), default);

        result.Outcome.Should().Be(RoleChangeOutcome.Forbidden);
        await identity.DidNotReceiveWithAnyArgs().RemoveFromRoleAsync(default!, default!);
    }

    [Fact]
    public async Task Revoke_should_refuse_to_remove_the_callers_own_system_admin_role()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var identity = Identity(User("admin-1", NewsroomRole.SystemAdmin), "admin-1", "admin-2");

        var result = await RevokeHandler(identity, ctx).Handle(
            new RevokeRoleCommand("admin-1", NewsroomRole.SystemAdmin, "admin-1", Admin), default);

        result.Outcome.Should().Be(RoleChangeOutcome.Conflict);
        await identity.DidNotReceiveWithAnyArgs().RemoveFromRoleAsync(default!, default!);
    }

    [Fact]
    public async Task Revoke_should_refuse_to_remove_the_last_system_admin()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var identity = Identity(User("other-admin", NewsroomRole.SystemAdmin), "other-admin");

        var result = await RevokeHandler(identity, ctx).Handle(
            new RevokeRoleCommand("other-admin", NewsroomRole.SystemAdmin, "admin-1", Admin), default);

        result.Outcome.Should().Be(RoleChangeOutcome.Conflict);
        (await ctx.RoleChangeAudits.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Revoke_of_system_admin_from_another_admin_should_work_when_others_remain()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var identity = Identity(User("admin-2", NewsroomRole.SystemAdmin), "admin-1", "admin-2");

        var result = await RevokeHandler(identity, ctx).Handle(
            new RevokeRoleCommand("admin-2", NewsroomRole.SystemAdmin, "admin-1", Admin), default);

        result.Outcome.Should().Be(RoleChangeOutcome.Success);
    }

    [Fact]
    public async Task Revoke_of_a_role_the_user_does_not_have_should_be_unchanged()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var identity = Identity(User("target"));

        var result = await RevokeHandler(identity, ctx).Handle(
            new RevokeRoleCommand("target", NewsroomRole.Reporter, "admin-1", Admin), default);

        result.Outcome.Should().Be(RoleChangeOutcome.Unchanged);
        (await ctx.RoleChangeAudits.CountAsync()).Should().Be(0);
    }

    // ---------- list ----------

    [Fact]
    public async Task List_should_return_users_for_chief_and_admin_and_null_for_others()
    {
        var identity = Substitute.For<IIdentityService>();
        identity.ListUsersAsync(Arg.Any<string?>(), 1, 20, Arg.Any<CancellationToken>())
            .Returns(new NewsroomUserPage(new[] { User("a", NewsroomRole.Reporter) }, 1));
        var handler = new ListNewsroomUsersQueryHandler(identity);

        (await handler.Handle(new ListNewsroomUsersQuery(null, 1, 20, Chief), default))!.TotalCount.Should().Be(1);
        (await handler.Handle(new ListNewsroomUsersQuery(null, 1, 20, Admin), default)).Should().NotBeNull();
        (await handler.Handle(new ListNewsroomUsersQuery(null, 1, 20, Managing), default)).Should().BeNull();
        (await handler.Handle(new ListNewsroomUsersQuery(null, 1, 20, Array.Empty<string>()), default)).Should().BeNull();
    }
}
