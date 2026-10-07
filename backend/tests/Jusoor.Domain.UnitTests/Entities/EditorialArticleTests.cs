using FluentAssertions;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Jusoor.Domain.Editorial;
using Xunit;

namespace Jusoor.Domain.UnitTests.Entities;

public class EditorialArticleTests
{
    private static EditorialArticle NewDraft(string owner = "user-1")
        => EditorialArticle.Create("عنوان الخبر", "ملخص", "نص الخبر الكامل", owner, null, null);

    [Fact]
    public void Presentation_desks_should_be_validated_deduplicated_and_kept_separate_from_SEO_taxonomy()
    {
        var article = NewDraft();
        article.SetPresentationDesks(new[] { "sports", "mughtarib", "sports" });

        article.PresentationDesks.Should().Equal("mughtarib", "sports");
        article.PrimaryCategoryId.Should().BeNull();
        article.SecondaryTags.Should().BeEmpty();
        EditorialDesk.Catalog.Should().ContainKey("opportunities");
        EditorialDesk.Catalog.Should().HaveCount(13);
    }

    [Fact]
    public void Presentation_desks_should_reject_unknown_values()
    {
        var act = () => NewDraft().SetPresentationDesks(new[] { "not-a-demo-desk" });
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_should_start_as_an_unpublished_draft_owned_by_the_author()
    {
        var article = NewDraft("author-7");

        article.Status.Should().Be(EditorialArticleStatus.Draft);
        article.OwnerUserId.Should().Be("author-7");
        article.PublishedAtUtc.Should().BeNull();
        article.PublishedByUserId.Should().BeNull();
    }

    [Theory]
    [InlineData("", "body")]
    [InlineData("   ", "body")]
    [InlineData("title", "")]
    [InlineData("title", "   ")]
    public void Create_should_reject_blank_title_or_body(string title, string body)
    {
        var act = () => EditorialArticle.Create(title, null, body, "u", null, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_should_reject_a_missing_owner()
    {
        var act = () => EditorialArticle.Create("t", null, "b", " ", null, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_should_reject_a_city_without_a_country()
    {
        var act = () => EditorialArticle.Create("t", null, "b", "u", null, Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_should_reject_an_overlong_title()
    {
        var act = () => EditorialArticle.Create(new string('x', EditorialArticle.TitleMaxLength + 1), null, "b", "u", null, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_should_trim_text_and_normalise_a_blank_summary_to_null()
    {
        var article = EditorialArticle.Create("  عنوان  ", "   ", "  نص  ", "u", null, null);

        article.Title.Should().Be("عنوان");
        article.Body.Should().Be("نص");
        article.Summary.Should().BeNull();
    }

    [Fact]
    public void Publish_should_set_status_time_and_publisher()
    {
        var article = NewDraft();
        var now = DateTimeOffset.UtcNow;

        article.Publish("editor-1", now);

        article.Status.Should().Be(EditorialArticleStatus.Published);
        article.PublishedAtUtc.Should().Be(now);
        article.PublishedByUserId.Should().Be("editor-1");
    }

    [Fact]
    public void Publish_twice_should_throw()
    {
        var article = NewDraft();
        article.Publish("editor-1", DateTimeOffset.UtcNow);

        var act = () => article.Publish("editor-1", DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Publish_should_require_an_attributed_user()
    {
        var act = () => NewDraft().Publish("", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Unpublish_should_return_to_draft_and_clear_publication_data()
    {
        var article = NewDraft();
        article.Publish("editor-1", DateTimeOffset.UtcNow);

        article.Unpublish();

        article.Status.Should().Be(EditorialArticleStatus.Draft);
        article.PublishedAtUtc.Should().BeNull();
        article.PublishedByUserId.Should().BeNull();
    }

    [Fact]
    public void Unpublish_a_draft_should_throw()
    {
        var act = () => NewDraft().Unpublish();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Republish_after_unpublish_should_record_a_fresh_time()
    {
        var article = NewDraft();
        var first = DateTimeOffset.UtcNow.AddHours(-2);
        var second = DateTimeOffset.UtcNow;
        article.Publish("e", first);
        article.Unpublish();

        article.Publish("e", second);

        article.PublishedAtUtc.Should().Be(second);
    }

    [Fact]
    public void UpdateContent_should_keep_status_and_owner()
    {
        var article = NewDraft("owner-1");

        article.UpdateContent("جديد", null, "نص جديد", null, null);

        article.Title.Should().Be("جديد");
        article.OwnerUserId.Should().Be("owner-1");
        article.Status.Should().Be(EditorialArticleStatus.Draft);
    }

    // ---------------- workflow (Slice 19, decision D1) ----------------

    [Fact]
    public void Happy_path_should_move_Draft_to_InReview_to_Approved_to_Published()
    {
        var article = NewDraft();

        article.SubmitForReview();
        article.Status.Should().Be(EditorialArticleStatus.InReview);
        article.Approve();
        article.Status.Should().Be(EditorialArticleStatus.Approved);
        article.Publish("editor-1", DateTimeOffset.UtcNow);
        article.Status.Should().Be(EditorialArticleStatus.Published);
    }

    [Fact]
    public void RequestRevision_should_return_an_article_in_review_or_approved_to_draft()
    {
        var inReview = NewDraft();
        inReview.SubmitForReview();
        inReview.RequestRevision();
        inReview.Status.Should().Be(EditorialArticleStatus.Draft);

        var approved = NewDraft();
        approved.SubmitForReview();
        approved.Approve();
        approved.RequestRevision();
        approved.Status.Should().Be(EditorialArticleStatus.Draft);
    }

    [Fact]
    public void Illegal_transitions_should_throw_and_leave_the_state_unchanged()
    {
        var draft = NewDraft();
        ((Action)draft.Approve).Should().Throw<InvalidOperationException>();
        ((Action)draft.RequestRevision).Should().Throw<InvalidOperationException>();
        draft.Status.Should().Be(EditorialArticleStatus.Draft);

        var inReview = NewDraft();
        inReview.SubmitForReview();
        ((Action)inReview.SubmitForReview).Should().Throw<InvalidOperationException>();
        inReview.Status.Should().Be(EditorialArticleStatus.InReview);

        var approved = NewDraft();
        approved.SubmitForReview();
        approved.Approve();
        ((Action)approved.Approve).Should().Throw<InvalidOperationException>();
        ((Action)approved.SubmitForReview).Should().Throw<InvalidOperationException>();

        var published = NewDraft();
        published.Publish("p", DateTimeOffset.UtcNow);
        ((Action)published.SubmitForReview).Should().Throw<InvalidOperationException>();
        ((Action)published.Approve).Should().Throw<InvalidOperationException>();
        ((Action)published.RequestRevision).Should().Throw<InvalidOperationException>();
        published.Status.Should().Be(EditorialArticleStatus.Published);
    }

    [Fact]
    public void Existing_status_values_must_never_change()
    {
        // The Status column is persisted as an int: renumbering would silently
        // corrupt every stored article.
        ((int)EditorialArticleStatus.Draft).Should().Be(1);
        ((int)EditorialArticleStatus.Published).Should().Be(2);
        ((int)EditorialArticleStatus.InReview).Should().Be(3);
        ((int)EditorialArticleStatus.Approved).Should().Be(4);
        // Slice 24 assigns the previously reserved value 5 to Scheduled.
        ((int)EditorialArticleStatus.Scheduled).Should().Be(5);
        ((int)EditorialArticleStatus.Retracted).Should().Be(6);
        ((int)EditorialArticleStatus.Archived).Should().Be(7);
        Enum.IsDefined(typeof(EditorialArticleStatus), 5).Should().BeTrue();
    }
}
