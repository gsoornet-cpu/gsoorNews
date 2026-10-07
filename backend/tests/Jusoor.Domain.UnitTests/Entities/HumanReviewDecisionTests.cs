using FluentAssertions;
using Jusoor.Domain.Entities;
using Xunit;

namespace Jusoor.Domain.UnitTests.Entities;

public class HumanReviewDecisionTests
{
    [Fact]
    public void Create_should_reject_missing_reviewer()
    {
        var act = () => HumanReviewDecision.Create(
            Guid.NewGuid(), reviewedByUserId: "", true, "clearly about an Egyptian in Dubai", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_should_reject_missing_reasoning()
    {
        var act = () => HumanReviewDecision.Create(
            Guid.NewGuid(), reviewedByUserId: "editor-1", true, reasoning: "  ", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_should_succeed_for_a_valid_decision()
    {
        var decision = HumanReviewDecision.Create(
            Guid.NewGuid(), "editor-1", true, "clearly about an Egyptian expatriate in Dubai", DateTimeOffset.UtcNow);

        decision.IsRelevant.Should().BeTrue();
        decision.ReviewedByUserId.Should().Be("editor-1");
        decision.Reasoning.Should().Be("clearly about an Egyptian expatriate in Dubai");
    }
}
