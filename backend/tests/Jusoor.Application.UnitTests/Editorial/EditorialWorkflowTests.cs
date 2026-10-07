using FluentAssertions;
using FluentValidation.TestHelper;
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
/// Slice 19 (decision D1): the workflow handlers end to end — state changes,
/// the append-only transition audit trail, and the deny paths. The access
/// matrix itself is covered row by row in EditorialArticleAccessTests.
/// </summary>
public class EditorialWorkflowTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly string[] Reporter = { NewsroomRole.Reporter };
    private static readonly string[] Copy = { NewsroomRole.CopyEditor };
    private static readonly string[] Senior = { NewsroomRole.SeniorEditor };
    private static readonly string[] Managing = { NewsroomRole.ManagingEditor };
    private static readonly string[] Chief = { NewsroomRole.EditorInChief };
    private static readonly string[] Admin = { NewsroomRole.SystemAdmin };

    [Fact]
    public void Break_glass_publish_and_unpublish_require_a_reason_of_at_least_ten_characters()
    {
        var publish = new PublishEditorialArticleCommandValidator();
        var unpublish = new UnpublishEditorialArticleCommandValidator();
        var id = Guid.NewGuid();
        publish.TestValidate(new PublishEditorialArticleCommand(id, "admin", Admin))
            .ShouldHaveValidationErrorFor(x => x.Reason);
        publish.TestValidate(new PublishEditorialArticleCommand(id, "admin", Admin, "  too short  "))
            .ShouldHaveValidationErrorFor(x => x.Reason);
        publish.TestValidate(new PublishEditorialArticleCommand(id, "admin", Admin, "Emergency legal takedown"))
            .ShouldNotHaveValidationErrorFor(x => x.Reason);
        unpublish.TestValidate(new UnpublishEditorialArticleCommand(id, "admin", Admin))
            .ShouldHaveValidationErrorFor(x => x.Reason);
        unpublish.TestValidate(new UnpublishEditorialArticleCommand(id, "admin", Admin, "Emergency legal takedown"))
            .ShouldNotHaveValidationErrorFor(x => x.Reason);
    }

    private static IDateTimeProvider Clock(DateTimeOffset now)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        return clock;
    }

    private static async Task<EditorialArticle> SeedDraftAsync(TestApplicationDbContext ctx, string owner = "rep-1")
    {
        var article = EditorialArticle.Create("خبر", "ملخص", "نص الخبر", owner, null, null);
        ctx.EditorialArticles.Add(article);
        await ctx.SaveChangesAsync(default);
        return article;
    }

    private static Task<EditorialArticleResult> Submit(TestApplicationDbContext ctx, Guid id, string user, string[] roles, DateTimeOffset? at = null)
        => new SubmitEditorialArticleCommandHandler(ctx, Clock(at ?? T0), NullLogger<SubmitEditorialArticleCommandHandler>.Instance)
            .Handle(new SubmitEditorialArticleCommand(id, user, roles), default);

    private static Task<EditorialArticleResult> Revise(TestApplicationDbContext ctx, Guid id, string user, string[] roles, string reason, DateTimeOffset? at = null)
        => new RequestEditorialRevisionCommandHandler(ctx, Clock(at ?? T0), NullLogger<RequestEditorialRevisionCommandHandler>.Instance)
            .Handle(new RequestEditorialRevisionCommand(id, reason, user, roles), default);

    private static Task<EditorialArticleResult> Approve(TestApplicationDbContext ctx, Guid id, string user, string[] roles, DateTimeOffset? at = null)
        => new ApproveEditorialArticleCommandHandler(ctx, Clock(at ?? T0), NullLogger<ApproveEditorialArticleCommandHandler>.Instance)
            .Handle(new ApproveEditorialArticleCommand(id, user, roles), default);

    private static Task<EditorialArticleResult> Publish(TestApplicationDbContext ctx, Guid id, string user, string[] roles, DateTimeOffset? at = null)
        => new PublishEditorialArticleCommandHandler(ctx, Clock(at ?? T0), NullLogger<PublishEditorialArticleCommandHandler>.Instance)
            .Handle(new PublishEditorialArticleCommand(id, user, roles), default);

    private static Task<EditorialArticleResult> Unpublish(TestApplicationDbContext ctx, Guid id, string user, string[] roles, DateTimeOffset? at = null)
        => new UnpublishEditorialArticleCommandHandler(ctx, Clock(at ?? T0), NullLogger<UnpublishEditorialArticleCommandHandler>.Instance)
            .Handle(new UnpublishEditorialArticleCommand(id, user, roles), default);

    private static Task<List<EditorialArticleTransition>> TrailAsync(TestApplicationDbContext ctx)
        => ctx.EditorialArticleTransitions.AsNoTracking().OrderBy(t => t.OccurredAtUtc).ToListAsync();

    [Fact]
    public async Task Full_workflow_should_move_an_article_through_every_state_and_record_each_step()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedDraftAsync(ctx);

        (await Submit(ctx, article.Id, "rep-1", Reporter, T0)).Outcome.Should().Be(EditorialOutcome.Success);
        (await Revise(ctx, article.Id, "copy-1", Copy, "أضف المصدر", T0.AddMinutes(1))).Outcome.Should().Be(EditorialOutcome.Success);
        (await Submit(ctx, article.Id, "rep-1", Reporter, T0.AddMinutes(2))).Outcome.Should().Be(EditorialOutcome.Success);
        (await Approve(ctx, article.Id, "senior-1", Senior, T0.AddMinutes(3))).Outcome.Should().Be(EditorialOutcome.Success);
        (await Publish(ctx, article.Id, "managing-1", Managing, T0.AddMinutes(4))).Outcome.Should().Be(EditorialOutcome.Success);

        var saved = await ctx.EditorialArticles.AsNoTracking().SingleAsync();
        saved.Status.Should().Be(EditorialArticleStatus.Published);
        saved.PublishedAtUtc.Should().Be(T0.AddMinutes(4));
        saved.PublishedByUserId.Should().Be("managing-1");

        var trail = await TrailAsync(ctx);
        trail.Select(t => $"{t.FromStatus}>{t.ToStatus}").Should().Equal(
            "Draft>InReview", "InReview>Draft", "Draft>InReview", "InReview>Approved", "Approved>Published");
        trail.Should().OnlyContain(t => t.ArticleId == article.Id);
        trail[1].Reason.Should().Be("أضف المصدر");
        trail[1].ActorUserId.Should().Be("copy-1");
        trail[1].ActorRoles.Should().Be(NewsroomRole.CopyEditor);
    }

    [Fact]
    public async Task An_article_is_public_only_once_published_never_while_in_review_or_approved()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedDraftAsync(ctx);
        var feed = new GetPublishedNewsQueryHandler(ctx);

        await Submit(ctx, article.Id, "rep-1", Reporter);
        (await feed.Handle(new GetPublishedNewsQuery(), default)).Items.Should().BeEmpty();
        await Approve(ctx, article.Id, "senior-1", Senior);
        (await feed.Handle(new GetPublishedNewsQuery(), default)).Items.Should().BeEmpty();
        (await new GetPublishedNewsByIdQueryHandler(ctx).Handle(new GetPublishedNewsByIdQuery(article.Id), default)).Outcome
            .Should().Be(Jusoor.Application.Editorial.Contracts.PublicNewsDetailOutcome.NotFound);

        await Publish(ctx, article.Id, "managing-1", Managing);
        (await feed.Handle(new GetPublishedNewsQuery(), default)).Items.Should().ContainSingle();
    }

    [Fact]
    public async Task A_managing_editor_cannot_publish_a_draft_and_nothing_is_recorded()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedDraftAsync(ctx);

        var result = await Publish(ctx, article.Id, "managing-1", Managing);

        result.Outcome.Should().Be(EditorialOutcome.Conflict);
        (await ctx.EditorialArticles.AsNoTracking().SingleAsync()).Status.Should().Be(EditorialArticleStatus.Draft);
        (await TrailAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task The_editor_in_chief_can_publish_straight_from_draft()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedDraftAsync(ctx);

        (await Publish(ctx, article.Id, "eic-1", Chief)).Outcome.Should().Be(EditorialOutcome.Success);

        var trail = await TrailAsync(ctx);
        trail.Should().ContainSingle().Which.ToStatus.Should().Be(EditorialArticleStatus.Published);
    }

    [Fact]
    public async Task System_admin_break_glass_actions_work_and_are_identifiable_in_the_trail()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedDraftAsync(ctx);

        (await Publish(ctx, article.Id, "admin-1", Admin, T0)).Outcome.Should().Be(EditorialOutcome.Success);
        (await Unpublish(ctx, article.Id, "admin-1", Admin, T0.AddMinutes(1))).Outcome.Should().Be(EditorialOutcome.Success);

        var trail = await TrailAsync(ctx);
        trail.Should().HaveCount(2);
        trail.Should().OnlyContain(t => t.ActorUserId == "admin-1" && t.ActorRoles == NewsroomRole.SystemAdmin);
    }

    [Fact]
    public async Task Denied_and_conflicting_actions_change_nothing_and_leave_no_audit_row()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedDraftAsync(ctx);
        await Submit(ctx, article.Id, "rep-1", Reporter);

        (await Approve(ctx, article.Id, "copy-1", Copy)).Outcome.Should().Be(EditorialOutcome.Forbidden);
        (await Publish(ctx, article.Id, "rep-1", Reporter)).Outcome.Should().Be(EditorialOutcome.Forbidden);
        (await Submit(ctx, article.Id, "rep-1", Reporter)).Outcome.Should().Be(EditorialOutcome.Conflict);
        (await Unpublish(ctx, article.Id, "managing-1", Managing)).Outcome.Should().Be(EditorialOutcome.Conflict);

        (await ctx.EditorialArticles.AsNoTracking().SingleAsync()).Status.Should().Be(EditorialArticleStatus.InReview);
        (await TrailAsync(ctx)).Should().ContainSingle(); // only the one successful submit
    }

    [Fact]
    public async Task A_reporter_cannot_submit_someone_elses_draft_and_it_looks_like_not_found()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedDraftAsync(ctx, owner: "rep-1");

        (await Submit(ctx, article.Id, "rep-2", Reporter)).Outcome.Should().Be(EditorialOutcome.NotFound);
        (await Submit(ctx, article.Id, "senior-1", Senior)).Outcome.Should().Be(EditorialOutcome.Success); // an editor may submit on the author's behalf
    }

    [Fact]
    public async Task A_copy_editor_cannot_withdraw_an_approval_but_a_senior_editor_can()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedDraftAsync(ctx);
        await Submit(ctx, article.Id, "rep-1", Reporter);
        await Approve(ctx, article.Id, "senior-1", Senior);

        (await Revise(ctx, article.Id, "copy-1", Copy, "x")).Outcome.Should().Be(EditorialOutcome.Forbidden);
        (await Revise(ctx, article.Id, "senior-1", Senior, "المصدر غير مؤكد")).Outcome.Should().Be(EditorialOutcome.Success);
        (await ctx.EditorialArticles.AsNoTracking().SingleAsync()).Status.Should().Be(EditorialArticleStatus.Draft);
    }

    [Fact]
    public async Task Workflow_actions_on_an_unknown_article_are_not_found()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();

        (await Approve(ctx, Guid.NewGuid(), "senior-1", Senior)).Outcome.Should().Be(EditorialOutcome.NotFound);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Request_revision_validator_should_require_a_reason(string reason)
    {
        var result = new RequestEditorialRevisionCommandValidator()
            .TestValidate(new RequestEditorialRevisionCommand(Guid.NewGuid(), reason, "u", Copy));

        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void Request_revision_validator_should_reject_an_overlong_reason_and_accept_a_normal_one()
    {
        var validator = new RequestEditorialRevisionCommandValidator();

        validator.TestValidate(new RequestEditorialRevisionCommand(Guid.NewGuid(), new string('x', 1001), "u", Copy))
            .ShouldHaveValidationErrorFor(x => x.Reason);
        validator.TestValidate(new RequestEditorialRevisionCommand(Guid.NewGuid(), "أضف المصدر", "u", Copy))
            .ShouldNotHaveAnyValidationErrors();
    }
}
