using FluentAssertions;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Xunit;

namespace Jusoor.Domain.UnitTests.Entities;

public class EditorialArticleScheduledPublicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private static EditorialArticle ApprovedArticle()
    {
        var article = EditorialArticle.Create("عنوان الخبر", "ملخص", "نص الخبر", "reporter-1", null, null);
        article.SubmitForReview();
        article.Approve();
        return article;
    }

    [Fact]
    public void Schedule_should_only_accept_an_approved_article_and_a_future_utc_time()
    {
        var article = ApprovedArticle();
        var due = Now.AddHours(2).ToOffset(TimeSpan.FromHours(3));

        article.SchedulePublication(due, Now);

        article.Status.Should().Be(EditorialArticleStatus.Scheduled);
        article.ScheduledPublishAtUtc.Should().Be(due.ToUniversalTime());
        article.PublishedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Schedule_should_reject_unapproved_or_non_future_articles()
    {
        var draft = EditorialArticle.Create("عنوان", null, "نص", "reporter-1", null, null);
        var past = ApprovedArticle();

        FluentActions.Invoking(() => draft.SchedulePublication(Now.AddHours(1), Now))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => past.SchedulePublication(Now, Now))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Cancel_should_return_to_approved_and_clear_the_due_time()
    {
        var article = ApprovedArticle();
        article.SchedulePublication(Now.AddHours(1), Now);

        article.CancelScheduledPublication();

        article.Status.Should().Be(EditorialArticleStatus.Approved);
        article.ScheduledPublishAtUtc.Should().BeNull();
        article.PublishedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Worker_publish_should_be_due_at_or_after_the_scheduled_time_and_clear_schedule()
    {
        var article = ApprovedArticle();
        var due = Now.AddMinutes(5);
        article.SchedulePublication(due, Now);

        FluentActions.Invoking(() => article.PublishScheduled("system:scheduled-publisher", due.AddTicks(-1)))
            .Should().Throw<InvalidOperationException>();

        article.PublishScheduled("system:scheduled-publisher", due.AddSeconds(10));

        article.Status.Should().Be(EditorialArticleStatus.Published);
        article.PublishedAtUtc.Should().Be(due.AddSeconds(10));
        article.PublishedByUserId.Should().Be("system:scheduled-publisher");
        article.ScheduledPublishAtUtc.Should().BeNull();
    }
}
