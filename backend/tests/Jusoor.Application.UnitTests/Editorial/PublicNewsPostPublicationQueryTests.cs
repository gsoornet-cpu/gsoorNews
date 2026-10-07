using System.Text.Json;
using FluentAssertions;
using Jusoor.Application.Editorial;
using Jusoor.Application.Editorial.Contracts;
using Jusoor.Application.UnitTests.TestSupport;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Xunit;

namespace Jusoor.Application.UnitTests.Editorial;

/// <summary>
/// Slice 20b, step 4 (decision Q9): what the PUBLIC read side does with the two new
/// post-publication states. The rules under test:
///   feed ...... Published only (so Archived and Retracted are in no list);
///   detail .... Published + Archived return the article (Archived flagged), Retracted
///               returns the public notice ONLY, every other state is not-found;
///   sitemap ... Published + Archived (flagged), never Retracted.
/// The most important assertions are the negative ones: a retracted article's body,
/// summary and corrections must not be reachable from any public result. Each such
/// assertion is paired with a positive control (the same marker IS present for a live
/// article), so a test cannot pass vacuously.
/// </summary>
public class PublicNewsPostPublicationQueryTests
{
    private static readonly DateTimeOffset PublishedAt = new(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ArchivedAt = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RetractedAt = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);

    private const string BodyMarker = "BODY-MARKER-must-never-leak";
    private const string SummaryMarker = "SUMMARY-MARKER-must-never-leak";
    private const string CorrectionMarker = "CORRECTION-MARKER-must-never-leak";
    private const string Notice = "تم سحب هذا الخبر لعدم دقة المعلومات الواردة فيه.";

    private static EditorialArticle PublishedArticle(string title = "عنوان الخبر", DateTimeOffset? publishedAt = null)
    {
        var article = EditorialArticle.Create(title, SummaryMarker, $"<p>{BodyMarker}</p>", "owner-1", null, null);
        article.Publish("publisher-1", publishedAt ?? PublishedAt);
        return article;
    }

    private static EditorialArticle ArchivedArticle(string title = "خبر مؤرشف", DateTimeOffset? publishedAt = null)
    {
        var article = PublishedArticle(title, publishedAt);
        article.Archive("chief-1", ArchivedAt);
        return article;
    }

    private static EditorialArticle RetractedArticle(string title = "خبر مسحوب", DateTimeOffset? publishedAt = null)
    {
        var article = PublishedArticle(title, publishedAt);
        article.Retract("chief-1", Notice, RetractedAt);
        return article;
    }

    private static async Task<TestApplicationDbContext> ContextWithAsync(params EditorialArticle[] articles)
    {
        var ctx = TestApplicationDbContext.CreateNew();
        ctx.EditorialArticles.AddRange(articles);
        await ctx.SaveChangesAsync(default);
        return ctx;
    }

    private static Task<PublicNewsDetailResult> Detail(TestApplicationDbContext ctx, Guid id)
        => new GetPublishedNewsByIdQueryHandler(ctx).Handle(new GetPublishedNewsByIdQuery(id), default);

    // ---------------- detail: Published / Archived ----------------

    [Fact]
    public async Task Detail_of_a_published_article_should_be_found_and_not_flagged_archived()
    {
        var article = PublishedArticle();
        await using var ctx = await ContextWithAsync(article);

        var result = await Detail(ctx, article.Id);

        result.Outcome.Should().Be(PublicNewsDetailOutcome.Found);
        result.Retraction.Should().BeNull();
        result.Article!.IsArchived.Should().BeFalse();
        result.Article.ArchivedAtUtc.Should().BeNull();
        result.Article.Body.Should().Contain(BodyMarker);
    }

    [Fact]
    public async Task Public_feed_can_filter_by_presentation_desk_without_exposing_unpublished_articles()
    {
        var sports = PublishedArticle("رياضة");
        sports.SetPresentationDesks(new[] { "sports" });
        var general = PublishedArticle("عام");
        general.SetPresentationDesks(new[] { "egypt" });
        var draft = EditorialArticle.Create("مسودة رياضية", null, "نص", "owner-1", null, null);
        draft.SetPresentationDesks(new[] { "sports" });
        await using var ctx = await ContextWithAsync(sports, general, draft);

        var result = await new GetPublishedNewsQueryHandler(ctx)
            .Handle(new GetPublishedNewsQuery(1, 20, "sports"), default);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Title.Should().Be("رياضة");
        result.Items.Single().PresentationDesks.Should().ContainSingle().Which.Should().Be("sports");
    }

    [Fact]
    public async Task Detail_of_an_archived_article_should_return_the_full_article_flagged_with_its_dates()
    {
        var article = ArchivedArticle();
        await using var ctx = await ContextWithAsync(article);

        var result = await Detail(ctx, article.Id);

        result.Outcome.Should().Be(PublicNewsDetailOutcome.Found);
        result.Retraction.Should().BeNull();
        result.Article!.IsArchived.Should().BeTrue();
        result.Article.ArchivedAtUtc.Should().Be(ArchivedAt);
        result.Article.PublishedAtUtc.Should().Be(PublishedAt); // "نُشر في …" keeps the real publication date
        result.Article.Title.Should().Be("خبر مؤرشف");
        result.Article.Body.Should().Contain(BodyMarker);
        result.Article.Summary.Should().Be(SummaryMarker);
    }

    [Fact]
    public async Task Detail_of_an_archived_article_should_still_list_its_corrections()
    {
        var article = ArchivedArticle();
        await using var ctx = await ContextWithAsync(article);
        ctx.EditorialCorrections.Add(EditorialCorrection.Issue(
            article.Id, CorrectionKind.Correction, "تصحيح لاحق", false, "senior-1", new[] { NewsroomRole.SeniorEditor }, ArchivedAt.AddHours(1)));
        await ctx.SaveChangesAsync(default);

        var result = await Detail(ctx, article.Id);

        result.Article!.Corrections.Should().ContainSingle().Which.Note.Should().Be("تصحيح لاحق");
    }

    [Fact]
    public async Task An_archived_article_restored_to_published_should_no_longer_be_flagged()
    {
        var article = ArchivedArticle();
        await using var ctx = await ContextWithAsync(article);
        article.RestoreFromArchive();
        await ctx.SaveChangesAsync(default);

        var result = await Detail(ctx, article.Id);

        result.Outcome.Should().Be(PublicNewsDetailOutcome.Found);
        result.Article!.IsArchived.Should().BeFalse();
        result.Article.ArchivedAtUtc.Should().BeNull();
        result.Article.PublishedAtUtc.Should().Be(PublishedAt);
    }

    // ---------------- detail: Retracted ----------------

    [Fact]
    public async Task Detail_of_a_retracted_article_should_return_only_the_public_notice()
    {
        var article = RetractedArticle();
        await using var ctx = await ContextWithAsync(article);

        var result = await Detail(ctx, article.Id);

        result.Outcome.Should().Be(PublicNewsDetailOutcome.Retracted);
        result.Article.Should().BeNull();
        result.Retraction.Should().NotBeNull();
        result.Retraction!.Id.Should().Be(article.Id);
        result.Retraction.Title.Should().Be("خبر مسحوب");
        result.Retraction.Notice.Should().Be(Notice);
        result.Retraction.PublishedAtUtc.Should().Be(PublishedAt); // the original publication stays part of the public record
        result.Retraction.RetractedAtUtc.Should().Be(RetractedAt);
    }

    [Fact]
    public async Task A_retracted_result_should_contain_no_body_summary_or_correction_text_anywhere()
    {
        var retracted = RetractedArticle();
        var live = PublishedArticle("خبر حي");
        await using var ctx = await ContextWithAsync(retracted, live);
        foreach (var id in new[] { retracted.Id, live.Id })
        {
            ctx.EditorialCorrections.Add(EditorialCorrection.Issue(
                id, CorrectionKind.Correction, CorrectionMarker, true, "senior-1", new[] { NewsroomRole.SeniorEditor }, PublishedAt.AddHours(1)));
        }

        await ctx.SaveChangesAsync(default);

        var retractedJson = JsonSerializer.Serialize(await Detail(ctx, retracted.Id));
        var liveJson = JsonSerializer.Serialize(await Detail(ctx, live.Id));

        // Positive control: the very same markers DO appear for a live article ...
        liveJson.Should().Contain(BodyMarker).And.Contain(SummaryMarker).And.Contain(CorrectionMarker);
        // ... and none of them appears for the retracted one.
        retractedJson.Should().NotContain(BodyMarker).And.NotContain(SummaryMarker).And.NotContain(CorrectionMarker);
    }

    [Fact]
    public void The_retraction_notice_dto_should_have_exactly_the_five_public_properties()
    {
        // The shape IS the guarantee. Adding a property here (Body, Summary, OwnerUserId,
        // RetractedByUserId ...) must be a deliberate, reviewed change that breaks this test.
        typeof(PublicRetractionNoticeDto).GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo("Id", "Title", "PublishedAtUtc", "RetractedAtUtc", "Notice");
    }

    [Fact]
    public void The_detail_result_factories_should_set_exactly_one_payload_for_their_own_outcome()
    {
        var article = new PublicNewsDetailDto(
            Guid.NewGuid(), "t", null, "<p>b</p>", PublishedAt, PublishedAt, null, null, null, null,
            Array.Empty<PublicCorrectionDto>(), false, null);
        var notice = new PublicRetractionNoticeDto(Guid.NewGuid(), "t", PublishedAt, RetractedAt, Notice);

        var found = PublicNewsDetailResult.Found(article);
        found.Outcome.Should().Be(PublicNewsDetailOutcome.Found);
        found.Article.Should().BeSameAs(article);
        found.Retraction.Should().BeNull();

        var retracted = PublicNewsDetailResult.Retracted(notice);
        retracted.Outcome.Should().Be(PublicNewsDetailOutcome.Retracted);
        retracted.Retraction.Should().BeSameAs(notice);
        retracted.Article.Should().BeNull();

        var missing = PublicNewsDetailResult.NotFound();
        missing.Outcome.Should().Be(PublicNewsDetailOutcome.NotFound);
        missing.Article.Should().BeNull();
        missing.Retraction.Should().BeNull();
    }

    [Fact]
    public async Task A_retracted_row_that_somehow_has_no_notice_should_fail_closed_as_not_found()
    {
        // The database check constraint and the domain make this row impossible; the in-memory
        // provider enforces neither, so this proves the QUERY is safe on its own: it shows
        // nothing rather than a partial page, and never falls through to the article text.
        var article = RetractedArticle();
        await using var ctx = await ContextWithAsync(article);
        ctx.Entry(article).Property(a => a.RetractionNotice).CurrentValue = null;
        await ctx.SaveChangesAsync(default);

        var result = await Detail(ctx, article.Id);

        result.Outcome.Should().Be(PublicNewsDetailOutcome.NotFound);
        result.Article.Should().BeNull();
        result.Retraction.Should().BeNull();
    }

    [Fact]
    public async Task A_reinstated_retracted_article_should_be_not_found_again_because_it_is_a_draft()
    {
        var article = RetractedArticle();
        await using var ctx = await ContextWithAsync(article);
        article.ReinstateRetracted();
        await ctx.SaveChangesAsync(default);

        var result = await Detail(ctx, article.Id);

        result.Outcome.Should().Be(PublicNewsDetailOutcome.NotFound);
    }

    // ---------------- detail: everything else stays invisible ----------------

    [Theory]
    [InlineData(EditorialArticleStatus.Draft)]
    [InlineData(EditorialArticleStatus.InReview)]
    [InlineData(EditorialArticleStatus.Approved)]
    public async Task Detail_of_a_non_public_state_should_be_not_found(EditorialArticleStatus status)
    {
        var article = EditorialArticle.Create("غير منشور", SummaryMarker, $"<p>{BodyMarker}</p>", "owner-1", null, null);
        switch (status)
        {
            case EditorialArticleStatus.InReview:
                article.SubmitForReview();
                break;
            case EditorialArticleStatus.Approved:
                article.SubmitForReview();
                article.Approve();
                break;
        }

        article.Status.Should().Be(status); // the seed really is in the state under test
        await using var ctx = await ContextWithAsync(article);

        var result = await Detail(ctx, article.Id);

        result.Outcome.Should().Be(PublicNewsDetailOutcome.NotFound);
        JsonSerializer.Serialize(result).Should().NotContain(BodyMarker);
    }

    [Fact]
    public async Task Detail_of_an_unknown_id_should_be_not_found()
    {
        await using var ctx = await ContextWithAsync(PublishedArticle());

        (await Detail(ctx, Guid.NewGuid())).Outcome.Should().Be(PublicNewsDetailOutcome.NotFound);
    }

    [Fact]
    public void Public_dtos_should_not_expose_who_retracted_or_archived()
    {
        var props = typeof(PublicNewsDetailDto).GetProperties().Select(p => p.Name)
            .Concat(typeof(PublicRetractionNoticeDto).GetProperties().Select(p => p.Name))
            .Concat(typeof(PublicNewsSummaryDto).GetProperties().Select(p => p.Name))
            .Concat(typeof(PublicNewsSitemapEntryDto).GetProperties().Select(p => p.Name));

        props.Should().NotContain(new[]
        {
            "RetractedByUserId", "ArchivedByUserId", "RetractionNotice", "OwnerUserId", "PublishedByUserId", "Status", "CreatedByUserId"
        });
    }

    // ---------------- feed: Published only ----------------

    [Fact]
    public async Task The_feed_should_list_only_published_articles_never_archived_or_retracted_ones()
    {
        var live = PublishedArticle("المنشور");
        var archived = ArchivedArticle("المؤرشف");
        var retracted = RetractedArticle("المسحوب");
        await using var ctx = await ContextWithAsync(live, archived, retracted);

        var feed = await new GetPublishedNewsQueryHandler(ctx).Handle(new GetPublishedNewsQuery(), default);

        feed.TotalCount.Should().Be(1);
        feed.Items.Should().ContainSingle().Which.Id.Should().Be(live.Id);
    }

    // ---------------- sitemap: Published + Archived (flagged), never Retracted ----------------

    [Fact]
    public async Task The_sitemap_should_include_published_and_flagged_archived_articles_but_never_retracted_ones()
    {
        var draft = EditorialArticle.Create("مسودة", null, "نص", "owner-1", null, null);
        var newest = PublishedArticle("الأحدث", PublishedAt);
        var archived = ArchivedArticle("المؤرشف", PublishedAt.AddDays(-2));
        var retracted = RetractedArticle("المسحوب", PublishedAt.AddDays(-1));
        var oldest = PublishedArticle("الأقدم", PublishedAt.AddDays(-5));
        await using var ctx = await ContextWithAsync(draft, newest, archived, retracted, oldest);

        var entries = await new GetPublishedNewsSitemapQueryHandler(ctx).Handle(new GetPublishedNewsSitemapQuery(), default);

        entries.Select(e => e.Title).Should().Equal("الأحدث", "المؤرشف", "الأقدم");
        entries.Select(e => e.Id).Should().NotContain(new[] { retracted.Id, draft.Id });
        entries.Single(e => e.Id == archived.Id).IsArchived.Should().BeTrue();
        entries.Single(e => e.Id == newest.Id).IsArchived.Should().BeFalse();
        entries.Single(e => e.Id == oldest.Id).IsArchived.Should().BeFalse();
        entries.Single(e => e.Id == archived.Id).PublishedAtUtc.Should().Be(PublishedAt.AddDays(-2));
    }

    [Fact]
    public async Task The_sitemap_should_drop_an_article_when_it_is_retracted_and_restore_the_flag_when_it_is_unarchived()
    {
        var article = PublishedArticle();
        await using var ctx = await ContextWithAsync(article);
        var handler = new GetPublishedNewsSitemapQueryHandler(ctx);

        (await handler.Handle(new GetPublishedNewsSitemapQuery(), default)).Single().IsArchived.Should().BeFalse();

        article.Archive("chief-1", ArchivedAt);
        await ctx.SaveChangesAsync(default);
        (await handler.Handle(new GetPublishedNewsSitemapQuery(), default)).Single().IsArchived.Should().BeTrue();

        article.RestoreFromArchive();
        await ctx.SaveChangesAsync(default);
        (await handler.Handle(new GetPublishedNewsSitemapQuery(), default)).Single().IsArchived.Should().BeFalse();

        article.Retract("chief-1", Notice, RetractedAt);
        await ctx.SaveChangesAsync(default);
        (await handler.Handle(new GetPublishedNewsSitemapQuery(), default)).Should().BeEmpty();
    }

    [Fact]
    public void The_sitemap_dto_should_have_exactly_the_six_lean_properties()
    {
        typeof(PublicNewsSitemapEntryDto).GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo("Id", "Title", "PublishedAtUtc", "UpdatedAtUtc", "IsArchived", "Slug");
    }
}
