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

/// <summary>
/// Slice 21 (decision D2): the handlers store ONLY what the sanitizer returns, reject a body
/// that is empty or oversized once cleaned, and read legacy plain-text articles as safe HTML.
/// The sanitizer's own policy is covered in Infrastructure.UnitTests; here it is a stub.
/// </summary>
public class EditorialRichTextHandlerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly string[] Reporter = { NewsroomRole.Reporter };
    private static readonly string[] Senior = { NewsroomRole.SeniorEditor };
    private static readonly string[] Admin = { NewsroomRole.SystemAdmin };

    private static IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(T0);
        return clock;
    }

    private static IEditorialHtmlSanitizer Sanitizer(Func<string, string>? transform = null)
    {
        var sanitizer = Substitute.For<IEditorialHtmlSanitizer>();
        sanitizer.Sanitize(Arg.Any<string>()).Returns(call => (transform ?? (s => s))(call.Arg<string>()));
        return sanitizer;
    }

    private static CreateEditorialArticleCommandHandler Creator(TestApplicationDbContext ctx, IEditorialHtmlSanitizer sanitizer)
        => new(ctx, sanitizer, NullLogger<CreateEditorialArticleCommandHandler>.Instance);

    private static UpdateEditorialArticleCommandHandler Updater(TestApplicationDbContext ctx, IEditorialHtmlSanitizer sanitizer)
        => new(ctx, Clock(), sanitizer, NullLogger<UpdateEditorialArticleCommandHandler>.Instance);

    private static CreateEditorialArticleCommand NewArticle(string body, string[]? roles = null)
        => new("عنوان", "ملخص", body, null, null, "rep-1", roles ?? Reporter);

    private static UpdateEditorialArticleCommand Edit(Guid id, string body, string user = "rep-1", string[]? roles = null, string? editReason = null)
        => new(id, "عنوان", null, body, null, null, user, roles ?? Reporter, editReason);

    /// <summary>Seeds an article, optionally rewinding it to how a pre-Slice-21 row looks (plain text, format 0).</summary>
    private static async Task<EditorialArticle> SeedAsync(
        TestApplicationDbContext ctx, string body, bool legacy = false, bool published = false)
    {
        var article = EditorialArticle.Create("عنوان", null, body, "rep-1", null, null);
        if (published)
        {
            article.Publish("pub-1", T0.AddHours(-1));
        }

        ctx.EditorialArticles.Add(article);
        await ctx.SaveChangesAsync(default);

        if (legacy)
        {
            ctx.Entry(article).Property(nameof(EditorialArticle.BodyFormat)).CurrentValue = ArticleBodyFormat.PlainText;
            await ctx.SaveChangesAsync(default);
        }

        return article;
    }

    // ---------------- create ----------------

    [Fact]
    public async Task Create_should_store_what_the_sanitizer_returns_not_what_was_submitted()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var sanitizer = Sanitizer(s => s.Replace("<script>x</script>", string.Empty));

        var result = await Creator(ctx, sanitizer).Handle(NewArticle("<p>نص</p><script>x</script>"), default);

        result.Outcome.Should().Be(EditorialOutcome.Success);
        result.Article!.Body.Should().Be("<p>نص</p>");
        var stored = await ctx.EditorialArticles.AsNoTracking().SingleAsync();
        stored.Body.Should().Be("<p>نص</p>");
        stored.BodyFormat.Should().Be(ArticleBodyFormat.Html);
        sanitizer.Received(1).Sanitize("<p>نص</p><script>x</script>");
    }

    [Theory]
    [InlineData("<p></p>")]
    [InlineData("<p><br></p>")]
    [InlineData("<p>&nbsp;</p>")]
    public async Task Create_should_reject_a_body_with_no_text_and_persist_nothing(string body)
    {
        await using var ctx = TestApplicationDbContext.CreateNew();

        var result = await Creator(ctx, Sanitizer()).Handle(NewArticle(body), default);

        result.Outcome.Should().Be(EditorialOutcome.InvalidBody);
        (await ctx.EditorialArticles.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_should_reject_a_body_that_sanitizing_made_too_long_instead_of_throwing()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var inflating = Sanitizer(s => s + new string('x', EditorialArticle.BodyMaxLength));

        var result = await Creator(ctx, inflating).Handle(NewArticle("<p>نص</p>"), default);

        result.Outcome.Should().Be(EditorialOutcome.InvalidBody);
        (await ctx.EditorialArticles.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_should_check_permission_before_looking_at_the_body()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var sanitizer = Sanitizer();

        var result = await Creator(ctx, sanitizer).Handle(NewArticle("<p></p>", Admin), default);

        result.Outcome.Should().Be(EditorialOutcome.Forbidden);
        sanitizer.DidNotReceive().Sanitize(Arg.Any<string>());
    }

    // ---------------- update ----------------

    [Fact]
    public async Task Update_should_store_what_the_sanitizer_returns()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, "<p>قديم</p>");
        var sanitizer = Sanitizer(s => s.Replace("<b>", string.Empty).Replace("</b>", string.Empty));

        var result = await Updater(ctx, sanitizer).Handle(Edit(article.Id, "<p><b>جديد</b></p>"), default);

        result.Outcome.Should().Be(EditorialOutcome.Success);
        result.Article!.Body.Should().Be("<p>جديد</p>");
        (await ctx.EditorialArticles.AsNoTracking().SingleAsync()).Body.Should().Be("<p>جديد</p>");
    }

    [Fact]
    public async Task Update_with_an_empty_body_should_change_nothing_and_not_create_a_revision()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, "<p>قديم</p>", published: true);

        var result = await Updater(ctx, Sanitizer()).Handle(Edit(article.Id, "<p></p>", "senior-1", Senior), default);

        result.Outcome.Should().Be(EditorialOutcome.InvalidBody);
        (await ctx.EditorialArticles.AsNoTracking().SingleAsync()).Body.Should().Be("<p>قديم</p>");
        (await ctx.EditorialArticleRevisions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Update_should_answer_not_found_and_forbidden_before_it_looks_at_the_body()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, "<p>نص</p>");
        var sanitizer = Sanitizer();

        var missing = await Updater(ctx, sanitizer).Handle(Edit(Guid.NewGuid(), "<p></p>"), default);
        var otherReporter = await Updater(ctx, sanitizer).Handle(Edit(article.Id, "<p></p>", "rep-2"), default);

        missing.Outcome.Should().Be(EditorialOutcome.NotFound);
        otherReporter.Outcome.Should().Be(EditorialOutcome.NotFound);
        sanitizer.DidNotReceive().Sanitize(Arg.Any<string>());
    }

    // ---------------- legacy plain-text articles ----------------

    [Fact]
    public async Task The_cms_should_receive_a_legacy_plain_text_body_as_encoded_html_paragraphs()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, "سطر ١\nسطر ٢\n\n<b>فقرة</b> & أخرى", legacy: true);

        var result = await new GetEditorialArticleQueryHandler(ctx)
            .Handle(new GetEditorialArticleQuery(article.Id, "rep-1", Reporter), default);

        result.Article!.Body.Should().Be("<p>سطر ١<br>سطر ٢</p><p>&lt;b&gt;فقرة&lt;/b&gt; &amp; أخرى</p>");
    }

    [Fact]
    public async Task The_public_page_should_receive_a_legacy_body_as_encoded_html_never_as_markup()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, "<script>alert(1)</script>", legacy: true, published: true);

        var detail = (await new GetPublishedNewsByIdQueryHandler(ctx).Handle(new GetPublishedNewsByIdQuery(article.Id), default)).Article;

        detail!.Body.Should().Be("<p>&lt;script&gt;alert(1)&lt;/script&gt;</p>");
        detail.Body.Should().NotContain("<script");
    }

    [Fact]
    public async Task Feed_excerpts_should_be_plain_text_for_both_formats()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var html = EditorialArticle.Create("ج", null, "<h2>عنوان فرعي</h2><p>نص <strong>غامق</strong></p>", "rep-1", null, null);
        html.Publish("pub-1", T0.AddHours(-2));
        var legacy = EditorialArticle.Create("ق", null, "سطر أول\n\nسطر ثان", "rep-1", null, null);
        legacy.Publish("pub-1", T0.AddHours(-1));
        ctx.EditorialArticles.AddRange(html, legacy);
        await ctx.SaveChangesAsync(default);
        ctx.Entry(legacy).Property(nameof(EditorialArticle.BodyFormat)).CurrentValue = ArticleBodyFormat.PlainText;
        await ctx.SaveChangesAsync(default);

        var feed = await new GetPublishedNewsQueryHandler(ctx).Handle(new GetPublishedNewsQuery(), default);

        feed.Items.Single(i => i.Id == html.Id).Excerpt.Should().Be("عنوان فرعي نص غامق");
        feed.Items.Single(i => i.Id == legacy.Id).Excerpt.Should().Be("سطر أول سطر ثان");
    }

    [Fact]
    public async Task Saving_a_legacy_article_should_convert_it_to_html_and_keep_the_old_snapshot_as_plain_text()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, "نص قديم", legacy: true, published: true);

        var result = await Updater(ctx, Sanitizer()).Handle(Edit(article.Id, "<p>نص جديد</p>", "senior-1", Senior, "تحويل تنسيق"), default);

        result.Outcome.Should().Be(EditorialOutcome.Success);
        var stored = await ctx.EditorialArticles.AsNoTracking().SingleAsync();
        stored.BodyFormat.Should().Be(ArticleBodyFormat.Html);
        var revision = await ctx.EditorialArticleRevisions.AsNoTracking().SingleAsync();
        revision.BodyFormat.Should().Be(ArticleBodyFormat.PlainText);
        revision.Body.Should().Be("نص قديم");

        var snapshot = await new GetEditorialArticleRevisionQueryHandler(ctx)
            .Handle(new GetEditorialArticleRevisionQuery(article.Id, 1, "senior-1", Senior), default);
        snapshot.Revision!.Body.Should().Be("<p>نص قديم</p>");
    }
}
