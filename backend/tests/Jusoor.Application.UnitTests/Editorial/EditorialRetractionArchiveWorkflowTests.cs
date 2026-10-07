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
/// Slice 20b, step 3 (decision Q9): the four post-publication command handlers and
/// their validators — state and field changes, exactly one append-only transition
/// row per success, nothing written on denied or conflicting attempts, and the
/// internal-reason / public-notice split. The Role x Action x State matrix itself
/// is covered exhaustively in EditorialArticleAccessTests; here we prove the
/// handlers honour it and persist correctly.
///
/// Deliberately NOT asserted here: what the PUBLIC detail query returns for an
/// Archived or Retracted article. That changes in step 4 (three-way result), and
/// pinning today's behaviour would only have to be rewritten. The feed is
/// different: GetPublishedNewsQuery stays Published-only by design, so absence
/// from the feed is asserted.
/// </summary>
public class EditorialRetractionArchiveWorkflowTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);
    private const string BodyMarker = "BODY-MARKER-do-not-leak";

    private static readonly string[] Reporter = { NewsroomRole.Reporter };
    private static readonly string[] Copy = { NewsroomRole.CopyEditor };
    private static readonly string[] Senior = { NewsroomRole.SeniorEditor };
    private static readonly string[] Managing = { NewsroomRole.ManagingEditor };
    private static readonly string[] Chief = { NewsroomRole.EditorInChief };
    private static readonly string[] Admin = { NewsroomRole.SystemAdmin };

    private const string Notice = "تم سحب هذا الخبر لعدم دقة المعلومات الواردة فيه.";
    private const string InternalReason = "المصدر الوحيد تراجع عن التصريح";

    private static IDateTimeProvider Clock(DateTimeOffset now)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        return clock;
    }

    private static async Task<EditorialArticle> SeedPublishedAsync(
        TestApplicationDbContext ctx, string owner = "rep-1", DateTimeOffset? publishedAt = null)
    {
        var article = EditorialArticle.Create("خبر", "ملخص", $"<p>{BodyMarker}</p>", owner, null, null);
        article.Publish("senior-0", publishedAt ?? T0.AddHours(-5));
        ctx.EditorialArticles.Add(article);
        await ctx.SaveChangesAsync(default);
        return article;
    }

    private static async Task<EditorialArticle> SeedDraftAsync(TestApplicationDbContext ctx, string owner = "rep-1")
    {
        var article = EditorialArticle.Create("خبر", "ملخص", "نص الخبر", owner, null, null);
        ctx.EditorialArticles.Add(article);
        await ctx.SaveChangesAsync(default);
        return article;
    }

    private static Task<EditorialArticleResult> Retract(
        TestApplicationDbContext ctx, Guid id, string user, string[] roles,
        string notice = Notice, string reason = InternalReason, DateTimeOffset? at = null)
        => new RetractEditorialArticleCommandHandler(ctx, Clock(at ?? T0), NullLogger<RetractEditorialArticleCommandHandler>.Instance)
            .Handle(new RetractEditorialArticleCommand(id, notice, reason, user, roles), default);

    private static Task<EditorialArticleResult> Archive(
        TestApplicationDbContext ctx, Guid id, string user, string[] roles, string? reason = null, DateTimeOffset? at = null)
        => new ArchiveEditorialArticleCommandHandler(ctx, Clock(at ?? T0), NullLogger<ArchiveEditorialArticleCommandHandler>.Instance)
            .Handle(new ArchiveEditorialArticleCommand(id, reason, user, roles), default);

    private static Task<EditorialArticleResult> Restore(
        TestApplicationDbContext ctx, Guid id, string user, string[] roles, string? reason = null, DateTimeOffset? at = null)
        => new RestoreEditorialArticleFromArchiveCommandHandler(ctx, Clock(at ?? T0), NullLogger<RestoreEditorialArticleFromArchiveCommandHandler>.Instance)
            .Handle(new RestoreEditorialArticleFromArchiveCommand(id, reason, user, roles), default);

    private static Task<EditorialArticleResult> Reinstate(
        TestApplicationDbContext ctx, Guid id, string user, string[] roles, string reason = "تم التحقق من الخبر", DateTimeOffset? at = null)
        => new ReinstateRetractedEditorialArticleCommandHandler(ctx, Clock(at ?? T0), NullLogger<ReinstateRetractedEditorialArticleCommandHandler>.Instance)
            .Handle(new ReinstateRetractedEditorialArticleCommand(id, reason, user, roles), default);

    private static Task<List<EditorialArticleTransition>> TrailAsync(TestApplicationDbContext ctx)
        => ctx.EditorialArticleTransitions.AsNoTracking().OrderBy(t => t.OccurredAtUtc).ToListAsync();

    private static Task<EditorialArticle> LoadAsync(TestApplicationDbContext ctx)
        => ctx.EditorialArticles.AsNoTracking().SingleAsync();

    // ---------------------------------------------------------------- Retract

    [Fact]
    public async Task Retract_should_move_a_published_article_to_retracted_and_keep_the_original_publication_time()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var publishedAt = T0.AddHours(-5);
        var article = await SeedPublishedAsync(ctx, publishedAt: publishedAt);

        var result = await Retract(ctx, article.Id, "senior-1", Senior);

        result.Outcome.Should().Be(EditorialOutcome.Success);
        var saved = await LoadAsync(ctx);
        saved.Status.Should().Be(EditorialArticleStatus.Retracted);
        saved.RetractedAtUtc.Should().Be(T0);
        saved.RetractedByUserId.Should().Be("senior-1");
        saved.RetractionNotice.Should().Be(Notice);
        saved.PublishedAtUtc.Should().Be(publishedAt);     // part of the public record
        saved.PublishedByUserId.Should().Be("senior-0");
    }

    [Fact]
    public async Task Retract_should_keep_the_public_notice_on_the_article_and_the_internal_reason_only_on_the_transition()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedPublishedAsync(ctx);

        await Retract(ctx, article.Id, "managing-1", Managing);

        var transition = (await TrailAsync(ctx)).Should().ContainSingle().Subject;
        transition.FromStatus.Should().Be(EditorialArticleStatus.Published);
        transition.ToStatus.Should().Be(EditorialArticleStatus.Retracted);
        transition.Reason.Should().Be(InternalReason);
        transition.ActorUserId.Should().Be("managing-1");
        transition.ActorRoles.Should().Be(NewsroomRole.ManagingEditor);
        transition.OccurredAtUtc.Should().Be(T0);

        // The two texts must never be swapped or merged.
        var saved = await LoadAsync(ctx);
        saved.RetractionNotice.Should().Be(Notice).And.NotContain(InternalReason);
    }

    [Fact]
    public async Task Retract_should_trim_the_notice_and_the_reason()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedPublishedAsync(ctx);

        await Retract(ctx, article.Id, "eic-1", Chief, notice: $"   {Notice}   ", reason: $"  {InternalReason}  ");

        (await LoadAsync(ctx)).RetractionNotice.Should().Be(Notice);
        (await TrailAsync(ctx)).Single().Reason.Should().Be(InternalReason);
    }

    [Fact]
    public async Task Retract_should_take_the_article_out_of_the_public_feed()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedPublishedAsync(ctx);
        var feed = new GetPublishedNewsQueryHandler(ctx);
        (await feed.Handle(new GetPublishedNewsQuery(), default)).Items.Should().ContainSingle();

        await Retract(ctx, article.Id, "senior-1", Senior);

        (await feed.Handle(new GetPublishedNewsQuery(), default)).Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData("copy")]
    [InlineData("admin")]
    public async Task Retract_by_a_role_that_may_not_should_be_forbidden_and_write_nothing(string who)
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedPublishedAsync(ctx);
        var (user, roles) = who == "copy" ? ("copy-1", Copy) : ("admin-1", Admin);

        var result = await Retract(ctx, article.Id, user, roles);

        result.Outcome.Should().Be(EditorialOutcome.Forbidden);
        var saved = await LoadAsync(ctx);
        saved.Status.Should().Be(EditorialArticleStatus.Published);
        saved.RetractionNotice.Should().BeNull();
        saved.RetractedAtUtc.Should().BeNull();
        (await TrailAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task Retract_by_the_owning_reporter_should_be_forbidden_and_by_another_reporter_not_found()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedPublishedAsync(ctx, owner: "rep-1");

        (await Retract(ctx, article.Id, "rep-1", Reporter)).Outcome.Should().Be(EditorialOutcome.Forbidden);
        (await Retract(ctx, article.Id, "rep-2", Reporter)).Outcome.Should().Be(EditorialOutcome.NotFound);

        (await LoadAsync(ctx)).Status.Should().Be(EditorialArticleStatus.Published);
        (await TrailAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task Retract_of_an_article_that_is_not_published_should_conflict_and_write_nothing()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var draft = await SeedDraftAsync(ctx);

        var result = await Retract(ctx, draft.Id, "senior-1", Senior);

        result.Outcome.Should().Be(EditorialOutcome.Conflict);
        var saved = await LoadAsync(ctx);
        saved.Status.Should().Be(EditorialArticleStatus.Draft);
        saved.RetractionNotice.Should().BeNull();
        (await TrailAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task Retracting_twice_should_conflict_the_second_time_and_keep_a_single_transition()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedPublishedAsync(ctx);

        (await Retract(ctx, article.Id, "senior-1", Senior, at: T0)).Outcome.Should().Be(EditorialOutcome.Success);
        (await Retract(ctx, article.Id, "senior-1", Senior, notice: "نص آخر مختلف تماماً", at: T0.AddMinutes(1)))
            .Outcome.Should().Be(EditorialOutcome.Conflict);

        (await LoadAsync(ctx)).RetractionNotice.Should().Be(Notice); // the first notice survives
        (await TrailAsync(ctx)).Should().ContainSingle();
    }

    // ---------------------------------------------------------------- Archive / Restore

    [Fact]
    public async Task Archive_should_move_a_published_article_to_archived_and_keep_the_publication_time()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var publishedAt = T0.AddDays(-30);
        var article = await SeedPublishedAsync(ctx, publishedAt: publishedAt);

        var result = await Archive(ctx, article.Id, "senior-1", Senior, reason: "خبر قديم");

        result.Outcome.Should().Be(EditorialOutcome.Success);
        var saved = await LoadAsync(ctx);
        saved.Status.Should().Be(EditorialArticleStatus.Archived);
        saved.ArchivedAtUtc.Should().Be(T0);
        saved.ArchivedByUserId.Should().Be("senior-1");
        saved.PublishedAtUtc.Should().Be(publishedAt);
        saved.RetractionNotice.Should().BeNull();

        var transition = (await TrailAsync(ctx)).Should().ContainSingle().Subject;
        transition.FromStatus.Should().Be(EditorialArticleStatus.Published);
        transition.ToStatus.Should().Be(EditorialArticleStatus.Archived);
        transition.Reason.Should().Be("خبر قديم");
    }

    [Fact]
    public async Task Archive_should_work_without_a_reason_and_record_none()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedPublishedAsync(ctx);

        (await Archive(ctx, article.Id, "managing-1", Managing, reason: null)).Outcome.Should().Be(EditorialOutcome.Success);

        (await TrailAsync(ctx)).Single().Reason.Should().BeNull();
    }

    [Fact]
    public async Task Archive_should_take_the_article_out_of_the_public_feed()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedPublishedAsync(ctx);
        var feed = new GetPublishedNewsQueryHandler(ctx);

        await Archive(ctx, article.Id, "senior-1", Senior);

        (await feed.Handle(new GetPublishedNewsQuery(), default)).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Restore_should_return_an_archived_article_to_published_with_its_original_time_and_back_into_the_feed()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var publishedAt = T0.AddDays(-30);
        var article = await SeedPublishedAsync(ctx, publishedAt: publishedAt);
        await Archive(ctx, article.Id, "senior-1", Senior, at: T0);

        var result = await Restore(ctx, article.Id, "managing-1", Managing, reason: "ما زال ذا صلة", at: T0.AddMinutes(5));

        result.Outcome.Should().Be(EditorialOutcome.Success);
        var saved = await LoadAsync(ctx);
        saved.Status.Should().Be(EditorialArticleStatus.Published);
        saved.ArchivedAtUtc.Should().BeNull();
        saved.ArchivedByUserId.Should().BeNull();
        saved.PublishedAtUtc.Should().Be(publishedAt);   // NOT reset to the restore time

        (await TrailAsync(ctx)).Select(t => $"{t.FromStatus}>{t.ToStatus}").Should().Equal("Published>Archived", "Archived>Published");
        (await new GetPublishedNewsQueryHandler(ctx).Handle(new GetPublishedNewsQuery(), default)).Items.Should().ContainSingle();
    }

    [Theory]
    [InlineData("copy")]
    [InlineData("admin")]
    public async Task Archive_and_restore_by_a_role_that_may_not_should_be_forbidden_and_write_nothing(string who)
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedPublishedAsync(ctx);
        var (user, roles) = who == "copy" ? ("copy-1", Copy) : ("admin-1", Admin);

        (await Archive(ctx, article.Id, user, roles)).Outcome.Should().Be(EditorialOutcome.Forbidden);
        (await LoadAsync(ctx)).Status.Should().Be(EditorialArticleStatus.Published);
        (await TrailAsync(ctx)).Should().BeEmpty();

        await Archive(ctx, article.Id, "senior-1", Senior);   // one legitimate transition so there is something to restore
        (await Restore(ctx, article.Id, user, roles)).Outcome.Should().Be(EditorialOutcome.Forbidden);

        (await LoadAsync(ctx)).Status.Should().Be(EditorialArticleStatus.Archived);
        (await TrailAsync(ctx)).Should().ContainSingle();      // only the legitimate archive
    }

    [Fact]
    public async Task Archive_of_a_draft_or_an_already_archived_or_retracted_article_should_conflict_and_write_nothing()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var draft = await SeedDraftAsync(ctx);
        (await Archive(ctx, draft.Id, "senior-1", Senior)).Outcome.Should().Be(EditorialOutcome.Conflict);

        var archived = await SeedPublishedAsync(ctx);
        await Archive(ctx, archived.Id, "senior-1", Senior);
        (await Archive(ctx, archived.Id, "senior-1", Senior)).Outcome.Should().Be(EditorialOutcome.Conflict);

        var retracted = await SeedPublishedAsync(ctx);
        await Retract(ctx, retracted.Id, "senior-1", Senior);
        (await Archive(ctx, retracted.Id, "senior-1", Senior)).Outcome.Should().Be(EditorialOutcome.Conflict);

        // Two successful transitions in total (archive, retract); the three conflicts wrote nothing.
        (await TrailAsync(ctx)).Should().HaveCount(2);
        (await ctx.EditorialArticles.AsNoTracking().SingleAsync(a => a.Id == draft.Id)).Status.Should().Be(EditorialArticleStatus.Draft);
        (await ctx.EditorialArticles.AsNoTracking().SingleAsync(a => a.Id == retracted.Id)).Status.Should().Be(EditorialArticleStatus.Retracted);
    }

    [Fact]
    public async Task Restore_of_a_published_or_retracted_article_should_conflict_and_write_nothing()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var published = await SeedPublishedAsync(ctx);
        (await Restore(ctx, published.Id, "senior-1", Senior)).Outcome.Should().Be(EditorialOutcome.Conflict);

        var retracted = await SeedPublishedAsync(ctx);
        await Retract(ctx, retracted.Id, "senior-1", Senior);
        (await Restore(ctx, retracted.Id, "eic-1", Chief)).Outcome.Should().Be(EditorialOutcome.Conflict);

        (await TrailAsync(ctx)).Should().ContainSingle();      // only the retract
        (await ctx.EditorialArticles.AsNoTracking().SingleAsync(a => a.Id == retracted.Id)).Status.Should().Be(EditorialArticleStatus.Retracted);
    }

    // ---------------------------------------------------------------- Reinstate

    [Fact]
    public async Task Reinstate_by_the_editor_in_chief_should_return_a_retracted_article_to_draft_and_clear_every_public_trace()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedPublishedAsync(ctx);
        await Retract(ctx, article.Id, "senior-1", Senior, at: T0);

        var result = await Reinstate(ctx, article.Id, "eic-1", Chief, reason: "تبيّن أن التصريح صحيح", at: T0.AddHours(1));

        result.Outcome.Should().Be(EditorialOutcome.Success);
        var saved = await LoadAsync(ctx);
        saved.Status.Should().Be(EditorialArticleStatus.Draft);
        saved.RetractedAtUtc.Should().BeNull();
        saved.RetractedByUserId.Should().BeNull();
        saved.RetractionNotice.Should().BeNull();
        saved.PublishedAtUtc.Should().BeNull();           // a later publish records a fresh time
        saved.PublishedByUserId.Should().BeNull();

        var trail = await TrailAsync(ctx);
        trail.Select(t => $"{t.FromStatus}>{t.ToStatus}").Should().Equal("Published>Retracted", "Retracted>Draft");
        trail[1].Reason.Should().Be("تبيّن أن التصريح صحيح");
        trail[1].ActorRoles.Should().Be(NewsroomRole.EditorInChief);

        (await new GetPublishedNewsQueryHandler(ctx).Handle(new GetPublishedNewsQuery(), default)).Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData("senior")]
    [InlineData("managing")]
    [InlineData("admin")]
    public async Task Reinstate_by_anyone_but_the_editor_in_chief_should_be_forbidden_and_leave_the_article_retracted(string who)
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedPublishedAsync(ctx);
        await Retract(ctx, article.Id, "senior-1", Senior);
        var (user, roles) = who switch
        {
            "senior" => ("senior-2", Senior),
            "managing" => ("managing-1", Managing),
            _ => ("admin-1", Admin)
        };

        var result = await Reinstate(ctx, article.Id, user, roles);

        result.Outcome.Should().Be(EditorialOutcome.Forbidden);
        var saved = await LoadAsync(ctx);
        saved.Status.Should().Be(EditorialArticleStatus.Retracted);
        saved.RetractionNotice.Should().Be(Notice);
        (await TrailAsync(ctx)).Should().ContainSingle();      // only the retract
    }

    [Fact]
    public async Task Reinstate_of_an_article_that_is_not_retracted_should_conflict_and_write_nothing()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var published = await SeedPublishedAsync(ctx);
        var archived = await SeedPublishedAsync(ctx);
        await Archive(ctx, archived.Id, "senior-1", Senior);

        (await Reinstate(ctx, published.Id, "eic-1", Chief)).Outcome.Should().Be(EditorialOutcome.Conflict);
        (await Reinstate(ctx, archived.Id, "eic-1", Chief)).Outcome.Should().Be(EditorialOutcome.Conflict);

        (await TrailAsync(ctx)).Should().ContainSingle();      // only the archive
    }

    [Fact]
    public async Task A_reinstated_article_should_be_publishable_again_with_a_fresh_time_and_no_stale_notice()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedPublishedAsync(ctx, publishedAt: T0.AddDays(-2));
        await Retract(ctx, article.Id, "senior-1", Senior, at: T0);
        await Reinstate(ctx, article.Id, "eic-1", Chief, at: T0.AddHours(1));

        var republish = await new PublishEditorialArticleCommandHandler(
                ctx, Clock(T0.AddHours(2)), NullLogger<PublishEditorialArticleCommandHandler>.Instance)
            .Handle(new PublishEditorialArticleCommand(article.Id, "eic-1", Chief), default);

        republish.Outcome.Should().Be(EditorialOutcome.Success);
        var saved = await LoadAsync(ctx);
        saved.Status.Should().Be(EditorialArticleStatus.Published);
        saved.PublishedAtUtc.Should().Be(T0.AddHours(2));
        saved.RetractionNotice.Should().BeNull();
        saved.RetractedAtUtc.Should().BeNull();
    }

    // ---------------------------------------------------------------- Shared

    [Fact]
    public async Task All_four_commands_on_an_unknown_article_should_be_not_found()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var id = Guid.NewGuid();

        (await Retract(ctx, id, "senior-1", Senior)).Outcome.Should().Be(EditorialOutcome.NotFound);
        (await Archive(ctx, id, "senior-1", Senior)).Outcome.Should().Be(EditorialOutcome.NotFound);
        (await Restore(ctx, id, "senior-1", Senior)).Outcome.Should().Be(EditorialOutcome.NotFound);
        (await Reinstate(ctx, id, "eic-1", Chief)).Outcome.Should().Be(EditorialOutcome.NotFound);
        (await TrailAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task The_full_life_cycle_should_leave_one_transition_row_per_step_with_the_right_actor_roles()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedPublishedAsync(ctx);

        await Archive(ctx, article.Id, "senior-1", Senior, at: T0);
        await Restore(ctx, article.Id, "managing-1", Managing, at: T0.AddMinutes(1));
        await Retract(ctx, article.Id, "senior-1", Senior, at: T0.AddMinutes(2));
        await Reinstate(ctx, article.Id, "eic-1", Chief, at: T0.AddMinutes(3));

        var trail = await TrailAsync(ctx);
        trail.Select(t => $"{t.FromStatus}>{t.ToStatus}").Should().Equal(
            "Published>Archived", "Archived>Published", "Published>Retracted", "Retracted>Draft");
        trail.Select(t => t.ActorRoles).Should().Equal(
            NewsroomRole.SeniorEditor, NewsroomRole.ManagingEditor, NewsroomRole.SeniorEditor, NewsroomRole.EditorInChief);
        trail.Should().OnlyContain(t => t.ArticleId == article.Id);
    }

    // ---------------------------------------------------------------- Validators

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("قصير")]                    // 4 characters
    [InlineData("123456789")]               // 9 characters
    [InlineData("   123456789   ")]         // 9 once trimmed: padding must not count
    public void Retract_validator_should_reject_a_missing_or_too_short_public_notice(string notice)
    {
        new RetractEditorialArticleCommandValidator()
            .TestValidate(new RetractEditorialArticleCommand(Guid.NewGuid(), notice, InternalReason, "u", Senior))
            .ShouldHaveValidationErrorFor(x => x.PublicNotice);
    }

    [Fact]
    public void Retract_validator_should_accept_a_notice_of_exactly_the_minimum_and_the_maximum_length()
    {
        var validator = new RetractEditorialArticleCommandValidator();

        validator.TestValidate(new RetractEditorialArticleCommand(Guid.NewGuid(), new string('x', 10), InternalReason, "u", Senior))
            .ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new RetractEditorialArticleCommand(Guid.NewGuid(), new string('x', 1000), InternalReason, "u", Senior))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Retract_validator_should_reject_an_overlong_public_notice()
    {
        new RetractEditorialArticleCommandValidator()
            .TestValidate(new RetractEditorialArticleCommand(Guid.NewGuid(), new string('x', 1001), InternalReason, "u", Senior))
            .ShouldHaveValidationErrorFor(x => x.PublicNotice);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Retract_validator_should_require_an_internal_reason(string reason)
    {
        new RetractEditorialArticleCommandValidator()
            .TestValidate(new RetractEditorialArticleCommand(Guid.NewGuid(), Notice, reason, "u", Senior))
            .ShouldHaveValidationErrorFor(x => x.InternalReason);
    }

    [Fact]
    public void Retract_validator_should_reject_an_overlong_internal_reason_and_accept_a_normal_command()
    {
        var validator = new RetractEditorialArticleCommandValidator();

        validator.TestValidate(new RetractEditorialArticleCommand(Guid.NewGuid(), Notice, new string('x', 1001), "u", Senior))
            .ShouldHaveValidationErrorFor(x => x.InternalReason);
        validator.TestValidate(new RetractEditorialArticleCommand(Guid.NewGuid(), Notice, InternalReason, "u", Senior))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Retract_validator_should_require_an_article_id_and_an_actor()
    {
        var result = new RetractEditorialArticleCommandValidator()
            .TestValidate(new RetractEditorialArticleCommand(Guid.Empty, Notice, InternalReason, "", Senior));

        result.ShouldHaveValidationErrorFor(x => x.ArticleId);
        result.ShouldHaveValidationErrorFor(x => x.ActorUserId);
    }

    [Fact]
    public void Archive_and_restore_validators_should_treat_the_reason_as_optional_but_bounded()
    {
        var archive = new ArchiveEditorialArticleCommandValidator();
        var restore = new RestoreEditorialArticleFromArchiveCommandValidator();

        archive.TestValidate(new ArchiveEditorialArticleCommand(Guid.NewGuid(), null, "u", Senior)).ShouldNotHaveAnyValidationErrors();
        archive.TestValidate(new ArchiveEditorialArticleCommand(Guid.NewGuid(), new string('x', 1000), "u", Senior)).ShouldNotHaveAnyValidationErrors();
        archive.TestValidate(new ArchiveEditorialArticleCommand(Guid.NewGuid(), new string('x', 1001), "u", Senior))
            .ShouldHaveValidationErrorFor(x => x.Reason);

        restore.TestValidate(new RestoreEditorialArticleFromArchiveCommand(Guid.NewGuid(), null, "u", Senior)).ShouldNotHaveAnyValidationErrors();
        restore.TestValidate(new RestoreEditorialArticleFromArchiveCommand(Guid.NewGuid(), new string('x', 1001), "u", Senior))
            .ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void Archive_and_restore_validators_should_require_an_article_id_and_an_actor()
    {
        new ArchiveEditorialArticleCommandValidator()
            .TestValidate(new ArchiveEditorialArticleCommand(Guid.Empty, null, "", Senior))
            .ShouldHaveValidationErrorFor(x => x.ArticleId);
        new RestoreEditorialArticleFromArchiveCommandValidator()
            .TestValidate(new RestoreEditorialArticleFromArchiveCommand(Guid.NewGuid(), null, "", Senior))
            .ShouldHaveValidationErrorFor(x => x.ActorUserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reinstate_validator_should_require_a_reason(string reason)
    {
        new ReinstateRetractedEditorialArticleCommandValidator()
            .TestValidate(new ReinstateRetractedEditorialArticleCommand(Guid.NewGuid(), reason, "u", Chief))
            .ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void Reinstate_validator_should_reject_an_overlong_reason_and_accept_a_normal_one()
    {
        var validator = new ReinstateRetractedEditorialArticleCommandValidator();

        validator.TestValidate(new ReinstateRetractedEditorialArticleCommand(Guid.NewGuid(), new string('x', 1001), "u", Chief))
            .ShouldHaveValidationErrorFor(x => x.Reason);
        validator.TestValidate(new ReinstateRetractedEditorialArticleCommand(Guid.NewGuid(), "تم التحقق", "u", Chief))
            .ShouldNotHaveAnyValidationErrors();
    }
}
