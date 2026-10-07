using FluentAssertions;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Xunit;

namespace Jusoor.Domain.UnitTests.Entities;

/// <summary>Slice 20b (Q9): the Retracted and Archived states and their four transitions.</summary>
public class EditorialArticleRetractionArchiveTests
{
    private static readonly DateTimeOffset PublishedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LaterAt = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private static EditorialArticle InState(EditorialArticleStatus status)
    {
        var a = EditorialArticle.Create("عنوان", "ملخص", "نص الخبر", "owner-1", null, null);
        switch (status)
        {
            case EditorialArticleStatus.InReview:
                a.SubmitForReview();
                break;
            case EditorialArticleStatus.Approved:
                a.SubmitForReview();
                a.Approve();
                break;
            case EditorialArticleStatus.Published:
                a.Publish("pub-1", PublishedAt);
                break;
            case EditorialArticleStatus.Retracted:
                a.Publish("pub-1", PublishedAt);
                a.Retract("chief-1", "تم سحب الخبر لعدم دقته", LaterAt);
                break;
            case EditorialArticleStatus.Archived:
                a.Publish("pub-1", PublishedAt);
                a.Archive("senior-1", LaterAt);
                break;
        }

        return a;
    }

    // ---------------- Retract ----------------

    [Fact]
    public void Retract_should_set_state_and_notice_and_keep_the_original_publication()
    {
        var a = InState(EditorialArticleStatus.Published);

        a.Retract("chief-1", "  تم سحب الخبر  ", LaterAt);

        a.Status.Should().Be(EditorialArticleStatus.Retracted);
        a.RetractedAtUtc.Should().Be(LaterAt);
        a.RetractedByUserId.Should().Be("chief-1");
        a.RetractionNotice.Should().Be("تم سحب الخبر");
        a.PublishedAtUtc.Should().Be(PublishedAt);
        a.PublishedByUserId.Should().Be("pub-1");
        a.ArchivedAtUtc.Should().BeNull();
    }

    [Theory]
    [InlineData(EditorialArticleStatus.Draft)]
    [InlineData(EditorialArticleStatus.InReview)]
    [InlineData(EditorialArticleStatus.Approved)]
    [InlineData(EditorialArticleStatus.Retracted)]
    [InlineData(EditorialArticleStatus.Archived)]
    public void Retract_should_only_work_from_published(EditorialArticleStatus from)
    {
        var a = InState(from);

        var act = () => a.Retract("chief-1", "notice text", LaterAt);

        act.Should().Throw<InvalidOperationException>();
        a.Status.Should().Be(from);
    }

    [Theory]
    [InlineData("", "notice")]
    [InlineData("  ", "notice")]
    [InlineData("chief-1", "")]
    [InlineData("chief-1", "   ")]
    public void Retract_should_require_an_actor_and_a_notice(string user, string notice)
    {
        var a = InState(EditorialArticleStatus.Published);

        var act = () => a.Retract(user, notice, LaterAt);

        act.Should().Throw<ArgumentException>();
        a.Status.Should().Be(EditorialArticleStatus.Published);
        a.RetractionNotice.Should().BeNull();
    }

    [Fact]
    public void Retract_should_cap_the_notice_length_after_trimming()
    {
        var tooLong = InState(EditorialArticleStatus.Published);
        FluentActions.Invoking(() => tooLong.Retract("c", new string('x', EditorialArticle.RetractionNoticeMaxLength + 1), LaterAt))
            .Should().Throw<ArgumentException>();
        tooLong.Status.Should().Be(EditorialArticleStatus.Published);

        var exact = InState(EditorialArticleStatus.Published);
        exact.Retract("c", "  " + new string('x', EditorialArticle.RetractionNoticeMaxLength) + "  ", LaterAt);
        exact.RetractionNotice!.Length.Should().Be(EditorialArticle.RetractionNoticeMaxLength);
    }

    // ---------------- Archive / Restore ----------------

    [Fact]
    public void Archive_should_set_state_and_keep_the_original_publication()
    {
        var a = InState(EditorialArticleStatus.Published);

        a.Archive("senior-1", LaterAt);

        a.Status.Should().Be(EditorialArticleStatus.Archived);
        a.ArchivedAtUtc.Should().Be(LaterAt);
        a.ArchivedByUserId.Should().Be("senior-1");
        a.PublishedAtUtc.Should().Be(PublishedAt);
        a.RetractionNotice.Should().BeNull();
    }

    [Theory]
    [InlineData(EditorialArticleStatus.Draft)]
    [InlineData(EditorialArticleStatus.InReview)]
    [InlineData(EditorialArticleStatus.Approved)]
    [InlineData(EditorialArticleStatus.Retracted)]
    [InlineData(EditorialArticleStatus.Archived)]
    public void Archive_should_only_work_from_published(EditorialArticleStatus from)
    {
        var a = InState(from);

        FluentActions.Invoking(() => a.Archive("senior-1", LaterAt)).Should().Throw<InvalidOperationException>();
        a.Status.Should().Be(from);
    }

    [Fact]
    public void Archive_should_require_an_actor()
    {
        var a = InState(EditorialArticleStatus.Published);

        FluentActions.Invoking(() => a.Archive(" ", LaterAt)).Should().Throw<ArgumentException>();
        a.Status.Should().Be(EditorialArticleStatus.Published);
    }

    [Fact]
    public void RestoreFromArchive_should_return_to_published_with_the_original_time_and_clear_the_archive_stamp()
    {
        var a = InState(EditorialArticleStatus.Archived);

        a.RestoreFromArchive();

        a.Status.Should().Be(EditorialArticleStatus.Published);
        a.ArchivedAtUtc.Should().BeNull();
        a.ArchivedByUserId.Should().BeNull();
        a.PublishedAtUtc.Should().Be(PublishedAt);
        a.PublishedByUserId.Should().Be("pub-1");
    }

    [Theory]
    [InlineData(EditorialArticleStatus.Draft)]
    [InlineData(EditorialArticleStatus.InReview)]
    [InlineData(EditorialArticleStatus.Approved)]
    [InlineData(EditorialArticleStatus.Published)]
    [InlineData(EditorialArticleStatus.Retracted)]
    public void RestoreFromArchive_should_only_work_from_archived(EditorialArticleStatus from)
    {
        var a = InState(from);

        FluentActions.Invoking(() => a.RestoreFromArchive()).Should().Throw<InvalidOperationException>();
        a.Status.Should().Be(from);
    }

    // ---------------- ReinstateRetracted ----------------

    [Fact]
    public void ReinstateRetracted_should_go_to_draft_and_clear_retraction_and_publication_fields()
    {
        var a = InState(EditorialArticleStatus.Retracted);

        a.ReinstateRetracted();

        a.Status.Should().Be(EditorialArticleStatus.Draft);
        a.RetractedAtUtc.Should().BeNull();
        a.RetractedByUserId.Should().BeNull();
        a.RetractionNotice.Should().BeNull();
        a.PublishedAtUtc.Should().BeNull();
        a.PublishedByUserId.Should().BeNull();
    }

    [Theory]
    [InlineData(EditorialArticleStatus.Draft)]
    [InlineData(EditorialArticleStatus.InReview)]
    [InlineData(EditorialArticleStatus.Approved)]
    [InlineData(EditorialArticleStatus.Published)]
    [InlineData(EditorialArticleStatus.Archived)]
    public void ReinstateRetracted_should_only_work_from_retracted(EditorialArticleStatus from)
    {
        var a = InState(from);

        FluentActions.Invoking(() => a.ReinstateRetracted()).Should().Throw<InvalidOperationException>();
        a.Status.Should().Be(from);
    }

    [Fact]
    public void A_reinstated_article_can_be_published_again_with_a_fresh_time_and_no_stale_notice()
    {
        var a = InState(EditorialArticleStatus.Retracted);
        a.ReinstateRetracted();
        var later = LaterAt.AddDays(2);

        a.Publish("pub-2", later);

        a.Status.Should().Be(EditorialArticleStatus.Published);
        a.PublishedAtUtc.Should().Be(later);
        a.RetractionNotice.Should().BeNull();
    }

    [Fact]
    public void An_article_can_be_archived_restored_and_then_retracted()
    {
        var a = InState(EditorialArticleStatus.Published);
        a.Archive("s", LaterAt);
        a.RestoreFromArchive();

        a.Retract("c", "notice text", LaterAt.AddDays(1));

        a.Status.Should().Be(EditorialArticleStatus.Retracted);
        a.ArchivedAtUtc.Should().BeNull();
    }

    // ---------------- other transitions must not touch the new states ----------------

    [Theory]
    [InlineData(EditorialArticleStatus.Retracted)]
    [InlineData(EditorialArticleStatus.Archived)]
    public void Publish_should_not_work_from_retracted_or_archived(EditorialArticleStatus from)
    {
        var a = InState(from);

        FluentActions.Invoking(() => a.Publish("pub-2", LaterAt)).Should().Throw<InvalidOperationException>();
        a.Status.Should().Be(from);
        a.PublishedAtUtc.Should().Be(PublishedAt);
    }

    [Theory]
    [InlineData(EditorialArticleStatus.Retracted)]
    [InlineData(EditorialArticleStatus.Archived)]
    public void The_older_transitions_should_all_reject_retracted_and_archived(EditorialArticleStatus from)
    {
        var a = InState(from);

        FluentActions.Invoking(() => a.Unpublish()).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => a.SubmitForReview()).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => a.Approve()).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => a.RequestRevision()).Should().Throw<InvalidOperationException>();
        a.Status.Should().Be(from);
    }
}
