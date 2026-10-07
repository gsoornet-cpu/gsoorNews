using FluentAssertions;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Xunit;

namespace Jusoor.Domain.UnitTests.Entities;

public class EditorialArticleTransitionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_should_record_article_states_actor_roles_reason_and_time()
    {
        var articleId = Guid.NewGuid();

        var transition = EditorialArticleTransition.Create(
            articleId, EditorialArticleStatus.InReview, EditorialArticleStatus.Draft,
            "copy-1", new[] { NewsroomRole.CopyEditor }, "  أضف المصدر  ", Now);

        transition.ArticleId.Should().Be(articleId);
        transition.FromStatus.Should().Be(EditorialArticleStatus.InReview);
        transition.ToStatus.Should().Be(EditorialArticleStatus.Draft);
        transition.ActorUserId.Should().Be("copy-1");
        transition.ActorRoles.Should().Be(NewsroomRole.CopyEditor);
        transition.Reason.Should().Be("أضف المصدر");
        transition.OccurredAtUtc.Should().Be(Now);
    }

    [Fact]
    public void Create_should_snapshot_multiple_roles_in_a_stable_order_and_normalise_a_blank_reason_to_null()
    {
        var transition = EditorialArticleTransition.Create(
            Guid.NewGuid(), EditorialArticleStatus.Draft, EditorialArticleStatus.InReview,
            "u-1", new[] { NewsroomRole.SeniorEditor, NewsroomRole.Reporter }, "   ", Now);

        transition.ActorRoles.Should().Be("Reporter,SeniorEditor");
        transition.Reason.Should().BeNull();
    }

    [Fact]
    public void Create_should_reject_a_missing_article_a_missing_actor_and_a_no_op_transition()
    {
        var roles = new[] { NewsroomRole.SeniorEditor };

        FluentActions.Invoking(() => EditorialArticleTransition.Create(
                Guid.Empty, EditorialArticleStatus.Draft, EditorialArticleStatus.InReview, "u", roles, null, Now))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => EditorialArticleTransition.Create(
                Guid.NewGuid(), EditorialArticleStatus.Draft, EditorialArticleStatus.InReview, " ", roles, null, Now))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => EditorialArticleTransition.Create(
                Guid.NewGuid(), EditorialArticleStatus.Draft, EditorialArticleStatus.Draft, "u", roles, null, Now))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_should_reject_an_overlong_reason()
    {
        FluentActions.Invoking(() => EditorialArticleTransition.Create(
                Guid.NewGuid(), EditorialArticleStatus.InReview, EditorialArticleStatus.Draft, "u",
                new[] { NewsroomRole.CopyEditor }, new string('x', EditorialArticleTransition.ReasonMaxLength + 1), Now))
            .Should().Throw<ArgumentException>();
    }
}
