using FluentAssertions;
using Jusoor.Application.Editorial;
using Jusoor.Application.Editorial.Contracts;
using Jusoor.Application.UnitTests.TestSupport;
using Jusoor.Domain.Entities;
using Xunit;

namespace Jusoor.Application.UnitTests.Editorial;

/// <summary>
/// Slice 18 (SEO foundation), backend half: the public sitemap feed and the
/// public "last updated" instant. The signal comes from append-only content
/// revisions and corrections; audit-only workflow timestamps do not move it.
/// </summary>
public class PublishedNewsSeoTests
{
    private static readonly DateTimeOffset PublishedAt = new(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);

    private static EditorialArticle Published(string title, DateTimeOffset publishedAt, DateTimeOffset? lastModified = null)
    {
        var article = EditorialArticle.Create(title, null, "نص الخبر", "owner-1", null, null);
        article.Publish("publisher-1", publishedAt);
        article.LastModifiedAtUtc = lastModified;
        return article;
    }

    [Fact]
    public async Task Sitemap_should_list_only_published_articles_newest_first()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();

        var draft = EditorialArticle.Create("مسودة", null, "نص", "owner-1", null, null);
        var unpublished = Published("أُلغي نشره", PublishedAt.AddDays(-1));
        unpublished.Unpublish();
        var older = Published("الأقدم", PublishedAt.AddDays(-5));
        var newer = Published("الأحدث", PublishedAt);
        ctx.EditorialArticles.AddRange(draft, unpublished, older, newer);
        await ctx.SaveChangesAsync(default);

        var entries = await new GetPublishedNewsSitemapQueryHandler(ctx).Handle(new GetPublishedNewsSitemapQuery(), default);

        entries.Select(e => e.Title).Should().Equal("الأحدث", "الأقدم");
        entries.Select(e => e.Id).Should().NotContain(new[] { draft.Id, unpublished.Id });
        entries[0].Id.Should().Be(newer.Id);
        entries[0].PublishedAtUtc.Should().Be(PublishedAt);
    }

    [Fact]
    public async Task Sitemap_should_be_empty_when_nothing_is_published()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        ctx.EditorialArticles.Add(EditorialArticle.Create("مسودة", null, "نص", "owner-1", null, null));
        await ctx.SaveChangesAsync(default);

        var entries = await new GetPublishedNewsSitemapQueryHandler(ctx).Handle(new GetPublishedNewsSitemapQuery(), default);

        entries.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null, 0d)]   // never modified after creation -> the publication time
    [InlineData(5d, 5d)]     // edited after publication -> the edit time
    [InlineData(-2d, 0d)]    // a modification stamp earlier than publication can never pull "updated" before "published"
    [InlineData(0d, 0d)]     // same instant -> the publication time
    public async Task Updated_at_should_be_the_later_of_published_and_last_content_revision_in_detail_and_sitemap(
        double? modifiedOffsetHours, double expectedOffsetHours)
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = Published(
            "عنوان", PublishedAt, modifiedOffsetHours is { } hours ? PublishedAt.AddHours(hours) : null);
        ctx.EditorialArticles.Add(article);
        if (modifiedOffsetHours is { } offset)
        {
            ctx.EditorialArticleRevisions.Add(EditorialArticleRevision.CaptureBeforeEdit(
                article, 1, "editor-1", new[] { "SeniorEditor" }, "تعديل موثق", PublishedAt.AddHours(offset)));
        }
        await ctx.SaveChangesAsync(default);
        var expected = PublishedAt.AddHours(expectedOffsetHours);

        var detail = (await new GetPublishedNewsByIdQueryHandler(ctx).Handle(new GetPublishedNewsByIdQuery(article.Id), default)).Article;
        var entries = await new GetPublishedNewsSitemapQueryHandler(ctx).Handle(new GetPublishedNewsSitemapQuery(), default);

        detail.Should().NotBeNull();
        detail!.PublishedAtUtc.Should().Be(PublishedAt);
        detail.UpdatedAtUtc.Should().Be(expected);
        entries.Should().ContainSingle().Which.UpdatedAtUtc.Should().Be(expected);
    }

    [Fact]
    public async Task A_public_correction_advances_date_modified_but_an_archive_transition_does_not()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var archived = Published("مؤرشف", PublishedAt, PublishedAt.AddDays(3));
        archived.Archive("editor-1", PublishedAt.AddDays(3));
        ctx.EditorialArticles.Add(archived);
        ctx.EditorialCorrections.Add(EditorialCorrection.Issue(
            archived.Id, Jusoor.Domain.Enums.CorrectionKind.Clarification, "توضيح معلومات الخبر", false,
            "editor-1", new[] { "ManagingEditor" }, PublishedAt.AddDays(2)));
        await ctx.SaveChangesAsync(default);

        var detail = (await new GetPublishedNewsByIdQueryHandler(ctx)
            .Handle(new GetPublishedNewsByIdQuery(archived.Id), default)).Article;
        var sitemap = await new GetPublishedNewsSitemapQueryHandler(ctx).Handle(new GetPublishedNewsSitemapQuery(), default);

        detail!.UpdatedAtUtc.Should().Be(PublishedAt.AddDays(2));
        sitemap.Should().ContainSingle().Which.UpdatedAtUtc.Should().Be(PublishedAt.AddDays(2));
    }

    [Fact]
    public void Sitemap_dto_should_not_expose_bodies_internal_user_ids_or_status()
    {
        var props = typeof(PublicNewsSitemapEntryDto).GetProperties().Select(p => p.Name);

        props.Should().NotContain(new[] { "Body", "Summary", "OwnerUserId", "PublishedByUserId", "Status", "CreatedByUserId" });
    }
}
