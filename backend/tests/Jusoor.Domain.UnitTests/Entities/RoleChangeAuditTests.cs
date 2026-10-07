using FluentAssertions;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Xunit;

namespace Jusoor.Domain.UnitTests.Entities;

public class RoleChangeAuditTests
{
    [Fact]
    public void Create_should_record_who_did_what_to_whom_and_when()
    {
        var now = DateTimeOffset.UtcNow;

        var audit = RoleChangeAudit.Create("admin-1", "user-9", NewsroomRole.SeniorEditor, RoleChangeAction.Granted, now);

        audit.ActorUserId.Should().Be("admin-1");
        audit.TargetUserId.Should().Be("user-9");
        audit.Role.Should().Be(NewsroomRole.SeniorEditor);
        audit.Action.Should().Be(RoleChangeAction.Granted);
        audit.OccurredAtUtc.Should().Be(now);
    }

    [Theory]
    [InlineData("", "u", "SeniorEditor")]
    [InlineData("a", " ", "SeniorEditor")]
    [InlineData("a", "u", "Superuser")]
    [InlineData("a", "u", "")]
    public void Create_should_reject_missing_parties_or_a_non_newsroom_role(string actor, string target, string role)
    {
        var act = () => RoleChangeAudit.Create(actor, target, role, RoleChangeAction.Revoked, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }
}
