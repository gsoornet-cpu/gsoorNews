using FluentAssertions;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Xunit;

namespace Jusoor.Domain.UnitTests.Entities;

public class RelevanceResultTests
{
    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Create_should_reject_confidence_score_outside_0_to_1(double score)
    {
        var act = () => RelevanceResult.Create(
            Guid.NewGuid(), null, true, (decimal)score, RelevanceMethod.RulesOnly,
            "reasons", "rules-v1", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_should_reject_missing_engine_version()
    {
        var act = () => RelevanceResult.Create(
            Guid.NewGuid(), null, true, 0.8m, RelevanceMethod.RulesOnly,
            "reasons", "", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_should_succeed_for_a_valid_verdict()
    {
        var result = RelevanceResult.Create(
            Guid.NewGuid(), Guid.NewGuid(), true, 0.85m, RelevanceMethod.RulesOnly,
            "mentions Egyptian embassy", "rules-v1", DateTimeOffset.UtcNow);

        result.IsRelevant.Should().BeTrue();
        result.ConfidenceScore.Should().Be(0.85m);
    }
}
