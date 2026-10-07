using FluentAssertions;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Editorial;
using Jusoor.Application.UnitTests.TestSupport;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Jusoor.Application.UnitTests.Editorial;

public class EditorialHandlerTests
{
    private static readonly string[] Reporter = { NewsroomRole.Reporter };
    private static readonly string[] Senior = { NewsroomRole.SeniorEditor };
    private static readonly string[] Managing = { NewsroomRole.ManagingEditor };
    private static readonly string[] Copy = { NewsroomRole.CopyEditor };
    private static readonly string[] Admin = { NewsroomRole.SystemAdmin };
    private static readonly string[] Researcher = { NewsroomRole.Researcher };

    private static IDateTimeProvider Clock(DateTimeOffset now)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        return clock;
    }

    private static async Task<EditorialArticle> SeedAsync(
        TestApplicationDbContext ctx, string owner, bool published = false, bool inReview = false)
    {
        var article = EditorialArticle.Create("عنوان", "ملخص", "نص الخبر", owner, null, null);
        if (inReview)
        {
            article.SubmitForReview();
        }

        if (published)
        {
            article.Publish("seed-publisher", DateTimeOffset.UtcNow);
        }

        ctx.EditorialArticles.Add(article);
        await ctx.SaveChangesAsync(default);
        return article;
    }

    private static CreateEditorialArticleCommandHandler CreateHandler(TestApplicationDbContext ctx)
        => new(ctx, PassThroughHtmlSanitizer.Instance, NullLogger<CreateEditorialArticleCommandHandler>.Instance);

    // ---------------- create ----------------

    [Fact]
    public async Task Create_should_persist_a_draft_owned_by_the_caller_not_public()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();

        var result = await CreateHandler(ctx).Handle(
            new CreateEditorialArticleCommand("عنوان", "ملخص", "نص", null, null, "reporter-1", Reporter), default);

        result.Outcome.Should().Be(EditorialOutcome.Success);
        result.Article!.Status.Should().Be("Draft");
        result.Article.OwnerUserId.Should().Be("reporter-1");
        (await ctx.EditorialArticles.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData(NewsroomRole.SystemAdmin)]
    [InlineData(NewsroomRole.Researcher)]
    [InlineData(NewsroomRole.CrisisEditor)]
    public async Task Create_should_be_forbidden_for_roles_without_edit_rights_and_persist_nothing(string role)
    {
        await using var ctx = TestApplicationDbContext.CreateNew();

        var result = await CreateHandler(ctx).Handle(
            new CreateEditorialArticleCommand("t", null, "b", null, null, "u", new[] { role }), default);

        result.Outcome.Should().Be(EditorialOutcome.Forbidden);
        (await ctx.EditorialArticles.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_should_reject_a_nonexistent_country()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();

        var result = await CreateHandler(ctx).Handle(
            new CreateEditorialArticleCommand("t", null, "b", Guid.NewGuid(), null, "u", Reporter), default);

        result.Outcome.Should().Be(EditorialOutcome.InvalidGeography);
    }

    [Fact]
    public async Task Create_should_reject_a_city_that_belongs_to_a_different_country()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var egypt = new Country { NameAr = "مصر", NameEn = "Egypt", IsoCode2 = "EG", Slug = "egypt" };
        var uae = new Country { NameAr = "الإمارات", NameEn = "UAE", IsoCode2 = "AE", Slug = "uae" };
        var dubai = new City { CountryId = uae.Id, NameAr = "دبي", NameEn = "Dubai", Slug = "dubai" };
        ctx.Countries.AddRange(egypt, uae);
        ctx.Cities.Add(dubai);
        await ctx.SaveChangesAsync(default);

        var mismatched = await CreateHandler(ctx).Handle(
            new CreateEditorialArticleCommand("t", null, "b", egypt.Id, dubai.Id, "u", Reporter), default);
        var matching = await CreateHandler(ctx).Handle(
            new CreateEditorialArticleCommand("t", null, "b", uae.Id, dubai.Id, "u", Reporter), default);

        mismatched.Outcome.Should().Be(EditorialOutcome.InvalidGeography);
        matching.Outcome.Should().Be(EditorialOutcome.Success);
    }

    [Theory]
    [InlineData("", "body")]
    [InlineData("   ", "body")]
    [InlineData("title", "")]
    public void Create_validator_should_reject_blank_title_or_body(string title, string body)
    {
        var validator = new CreateEditorialArticleCommandValidator();

        validator.Validate(new CreateEditorialArticleCommand(title, null, body, null, null, "u", Reporter))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Create_validator_should_reject_a_city_without_a_country_and_overlong_fields()
    {
        var validator = new CreateEditorialArticleCommandValidator();

        validator.Validate(new CreateEditorialArticleCommand("t", null, "b", null, Guid.NewGuid(), "u", Reporter))
            .IsValid.Should().BeFalse();
        validator.Validate(new CreateEditorialArticleCommand(new string('x', 301), null, "b", null, null, "u", Reporter))
            .IsValid.Should().BeFalse();
        validator.Validate(new CreateEditorialArticleCommand("t", new string('x', 501), "b", null, null, "u", Reporter))
            .IsValid.Should().BeFalse();
    }

    // ---------------- update ----------------

    private static UpdateEditorialArticleCommandHandler UpdateHandler(TestApplicationDbContext ctx)
        => new(ctx, Clock(new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero)), PassThroughHtmlSanitizer.Instance, NullLogger<UpdateEditorialArticleCommandHandler>.Instance);

    [Fact]
    public async Task Update_should_let_a_reporter_edit_their_own_draft_and_persist_it()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, "reporter-1");

        var result = await UpdateHandler(ctx).Handle(
            new UpdateEditorialArticleCommand(article.Id, "عنوان معدّل", null, "نص معدّل", null, null, "reporter-1", Reporter), default);

        result.Outcome.Should().Be(EditorialOutcome.Success);
        (await ctx.EditorialArticles.AsNoTracking().SingleAsync()).Title.Should().Be("عنوان معدّل");
    }

    [Fact]
    public async Task Update_by_a_reporter_on_someone_elses_article_should_look_like_not_found_and_change_nothing()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, "reporter-1");

        var result = await UpdateHandler(ctx).Handle(
            new UpdateEditorialArticleCommand(article.Id, "اختراق", null, "نص", null, null, "reporter-2", Reporter), default);

        result.Outcome.Should().Be(EditorialOutcome.NotFound);
        (await ctx.EditorialArticles.AsNoTracking().SingleAsync()).Title.Should().Be("عنوان");
    }

    [Fact]
    public async Task Update_by_system_admin_should_be_forbidden_because_admin_has_no_edit_right()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, "reporter-1");

        var result = await UpdateHandler(ctx).Handle(
            new UpdateEditorialArticleCommand(article.Id, "t", null, "b", null, null, "admin-1", Admin), default);

        result.Outcome.Should().Be(EditorialOutcome.Forbidden);
    }

    [Fact]
    public async Task Update_by_a_reporter_should_be_forbidden_once_their_article_is_published()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, "reporter-1", published: true);

        var result = await UpdateHandler(ctx).Handle(
            new UpdateEditorialArticleCommand(article.Id, "تعديل بعد النشر", null, "نص", null, null, "reporter-1", Reporter), default);

        result.Outcome.Should().Be(EditorialOutcome.Forbidden);
        (await ctx.EditorialArticles.AsNoTracking().SingleAsync()).Title.Should().Be("عنوان");
    }

    [Fact]
    public async Task Update_should_return_not_found_for_an_unknown_id()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();

        var result = await UpdateHandler(ctx).Handle(
            new UpdateEditorialArticleCommand(Guid.NewGuid(), "t", null, "b", null, null, "u", Managing), default);

        result.Outcome.Should().Be(EditorialOutcome.NotFound);
    }

    // ---------------- publish / unpublish ----------------

    private static PublishEditorialArticleCommandHandler PublishHandler(TestApplicationDbContext ctx, DateTimeOffset now)
        => new(ctx, Clock(now), NullLogger<PublishEditorialArticleCommandHandler>.Instance);

    private static UnpublishEditorialArticleCommandHandler UnpublishHandler(TestApplicationDbContext ctx)
        => new(ctx, Clock(DateTimeOffset.UtcNow), NullLogger<UnpublishEditorialArticleCommandHandler>.Instance);

    [Fact]
    public async Task Publish_by_a_managing_editor_should_publish_an_article_in_review_with_a_recorded_time_and_publisher()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, "reporter-1", inReview: true);
        var now = new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

        var result = await PublishHandler(ctx, now).Handle(
            new PublishEditorialArticleCommand(article.Id, "managing-1", Managing), default);

        result.Outcome.Should().Be(EditorialOutcome.Success);
        var saved = await ctx.EditorialArticles.AsNoTracking().SingleAsync();
        saved.Status.Should().Be(EditorialArticleStatus.Published);
        saved.PublishedAtUtc.Should().Be(now);
        saved.PublishedByUserId.Should().Be("managing-1");
    }

    [Fact]
    public async Task Publish_by_a_senior_editor_should_work_on_any_article_in_review_but_not_on_a_draft()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var othersInReview = await SeedAsync(ctx, "reporter-9", inReview: true);
        var othersDraft = await SeedAsync(ctx, "reporter-8");
        var handler = PublishHandler(ctx, DateTimeOffset.UtcNow);

        var reviewed = await handler.Handle(new PublishEditorialArticleCommand(othersInReview.Id, "senior-1", Senior), default);
        var draft = await handler.Handle(new PublishEditorialArticleCommand(othersDraft.Id, "senior-1", Senior), default);

        reviewed.Outcome.Should().Be(EditorialOutcome.Success);
        draft.Outcome.Should().Be(EditorialOutcome.Conflict);
        (await ctx.EditorialArticles.AsNoTracking().SingleAsync(a => a.Id == othersDraft.Id))
            .Status.Should().Be(EditorialArticleStatus.Draft);
    }

    [Theory]
    [InlineData(NewsroomRole.Reporter)]
    [InlineData(NewsroomRole.CopyEditor)]
    [InlineData(NewsroomRole.AiEditor)]
    public async Task Publish_should_be_forbidden_for_roles_without_publish_rights_even_on_their_own_article(string role)
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, "u-1");

        var result = await PublishHandler(ctx, DateTimeOffset.UtcNow).Handle(
            new PublishEditorialArticleCommand(article.Id, "u-1", new[] { role }), default);

        result.Outcome.Should().Be(EditorialOutcome.Forbidden);
        (await ctx.EditorialArticles.AsNoTracking().SingleAsync()).Status.Should().Be(EditorialArticleStatus.Draft);
    }

    [Fact]
    public async Task Publish_by_system_admin_should_be_allowed()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, "reporter-1");

        var result = await PublishHandler(ctx, DateTimeOffset.UtcNow).Handle(
            new PublishEditorialArticleCommand(article.Id, "admin-1", Admin), default);

        result.Outcome.Should().Be(EditorialOutcome.Success);
    }

    [Fact]
    public async Task Publish_by_an_uninvolved_role_should_look_like_not_found()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, "reporter-1");

        var result = await PublishHandler(ctx, DateTimeOffset.UtcNow).Handle(
            new PublishEditorialArticleCommand(article.Id, "r-1", Researcher), default);

        result.Outcome.Should().Be(EditorialOutcome.NotFound);
    }

    [Fact]
    public async Task Publish_twice_should_conflict_and_keep_the_original_publication_time()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, "reporter-1", inReview: true);
        var first = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

        await PublishHandler(ctx, first).Handle(new PublishEditorialArticleCommand(article.Id, "m", Managing), default);
        var second = await PublishHandler(ctx, first.AddHours(3)).Handle(
            new PublishEditorialArticleCommand(article.Id, "m", Managing), default);

        second.Outcome.Should().Be(EditorialOutcome.Conflict);
        (await ctx.EditorialArticles.AsNoTracking().SingleAsync()).PublishedAtUtc.Should().Be(first);
    }

    [Fact]
    public async Task Unpublish_should_return_the_article_to_draft_and_hide_it_from_the_public_feed()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, "reporter-1", published: true);

        var result = await UnpublishHandler(ctx).Handle(new UnpublishEditorialArticleCommand(article.Id, "m", Managing), default);

        result.Outcome.Should().Be(EditorialOutcome.Success);
        var feed = await new GetPublishedNewsQueryHandler(ctx).Handle(new GetPublishedNewsQuery(), default);
        feed.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Unpublish_by_a_copy_editor_should_be_forbidden_and_leave_the_article_live()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, "reporter-1", published: true);

        var result = await UnpublishHandler(ctx).Handle(new UnpublishEditorialArticleCommand(article.Id, "c", Copy), default);

        result.Outcome.Should().Be(EditorialOutcome.Forbidden);
        (await ctx.EditorialArticles.AsNoTracking().SingleAsync()).Status.Should().Be(EditorialArticleStatus.Published);
    }

    [Fact]
    public async Task Unpublish_a_draft_should_conflict()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, "reporter-1");

        var result = await UnpublishHandler(ctx).Handle(new UnpublishEditorialArticleCommand(article.Id, "m", Managing), default);

        result.Outcome.Should().Be(EditorialOutcome.Conflict);
    }

    // ---------------- CMS reads ----------------

    [Fact]
    public async Task List_for_a_reporter_should_return_only_their_own_articles()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        await SeedAsync(ctx, "reporter-1");
        await SeedAsync(ctx, "reporter-2");

        var result = await new ListEditorialArticlesQueryHandler(ctx).Handle(
            new ListEditorialArticlesQuery(1, 20, null, "reporter-1", Reporter), default);

        result.TotalCount.Should().Be(1);
        result.Items.Single().OwnerUserId.Should().Be("reporter-1");
    }

    [Fact]
    public async Task List_for_a_managing_editor_should_return_everything_and_filter_by_status()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        await SeedAsync(ctx, "reporter-1");
        await SeedAsync(ctx, "reporter-2", published: true);
        var handler = new ListEditorialArticlesQueryHandler(ctx);

        var all = await handler.Handle(new ListEditorialArticlesQuery(1, 20, null, "m", Managing), default);
        var drafts = await handler.Handle(new ListEditorialArticlesQuery(1, 20, EditorialArticleStatus.Draft, "m", Managing), default);

        all.TotalCount.Should().Be(2);
        drafts.Items.Should().ContainSingle().Which.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task List_for_a_role_without_cms_access_should_return_nothing()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        await SeedAsync(ctx, "reporter-1");

        var result = await new ListEditorialArticlesQueryHandler(ctx).Handle(
            new ListEditorialArticlesQuery(1, 20, null, "x", Researcher), default);

        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Get_should_hide_another_reporters_draft_as_not_found()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, "reporter-1");
        var handler = new GetEditorialArticleQueryHandler(ctx);

        var owner = await handler.Handle(new GetEditorialArticleQuery(article.Id, "reporter-1", Reporter), default);
        var stranger = await handler.Handle(new GetEditorialArticleQuery(article.Id, "reporter-2", Reporter), default);

        owner.Outcome.Should().Be(EditorialOutcome.Success);
        stranger.Outcome.Should().Be(EditorialOutcome.NotFound);
    }

    // ---------------- public reads ----------------

    [Fact]
    public async Task Public_feed_should_contain_only_published_articles_newest_first()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        await SeedAsync(ctx, "r", published: false);
        var older = EditorialArticle.Create("الأقدم", null, "نص", "r", null, null);
        older.Publish("m", new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        var newer = EditorialArticle.Create("الأحدث", null, "نص", "r", null, null);
        newer.Publish("m", new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero));
        ctx.EditorialArticles.AddRange(older, newer);
        await ctx.SaveChangesAsync(default);

        var feed = await new GetPublishedNewsQueryHandler(ctx).Handle(new GetPublishedNewsQuery(), default);

        feed.TotalCount.Should().Be(2);
        feed.Items.Select(i => i.Title).Should().Equal("الأحدث", "الأقدم");
    }

    [Fact]
    public async Task Public_detail_should_return_not_found_for_a_draft_and_the_article_once_published()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var draft = await SeedAsync(ctx, "r");
        var live = await SeedAsync(ctx, "r", published: true);
        var handler = new GetPublishedNewsByIdQueryHandler(ctx);

        (await handler.Handle(new GetPublishedNewsByIdQuery(draft.Id), default)).Outcome
            .Should().Be(Jusoor.Application.Editorial.Contracts.PublicNewsDetailOutcome.NotFound);
        var detail = (await handler.Handle(new GetPublishedNewsByIdQuery(live.Id), default)).Article;
        detail.Should().NotBeNull();
        detail!.Body.Should().Be("نص الخبر");
    }

    [Fact]
    public async Task Public_feed_should_fall_back_to_a_truncated_body_when_there_is_no_summary()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = EditorialArticle.Create("عنوان", null, new string('ا', 400), "r", null, null);
        article.Publish("m", DateTimeOffset.UtcNow);
        ctx.EditorialArticles.Add(article);
        await ctx.SaveChangesAsync(default);

        var feed = await new GetPublishedNewsQueryHandler(ctx).Handle(new GetPublishedNewsQuery(), default);

        feed.Items.Single().Excerpt!.Length.Should().BeLessThan(400);
    }

    [Fact]
    public void Public_dtos_should_not_expose_internal_user_ids_or_status()
    {
        var publicProps = typeof(Jusoor.Application.Editorial.Contracts.PublicNewsDetailDto).GetProperties().Select(p => p.Name)
            .Concat(typeof(Jusoor.Application.Editorial.Contracts.PublicNewsSummaryDto).GetProperties().Select(p => p.Name));

        publicProps.Should().NotContain(new[] { "OwnerUserId", "PublishedByUserId", "Status", "CreatedByUserId" });
    }
}
