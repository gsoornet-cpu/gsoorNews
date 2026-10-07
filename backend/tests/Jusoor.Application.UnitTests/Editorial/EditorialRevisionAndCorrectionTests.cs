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
/// Slice 20 (decision D4): revision snapshots on every edit of a published
/// article, public corrections/notices, and the history queries.
/// </summary>
public class EditorialRevisionAndCorrectionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly string[] Reporter = { NewsroomRole.Reporter };
    private static readonly string[] Copy = { NewsroomRole.CopyEditor };
    private static readonly string[] Senior = { NewsroomRole.SeniorEditor };
    private static readonly string[] Managing = { NewsroomRole.ManagingEditor };
    private static readonly string[] Chief = { NewsroomRole.EditorInChief };
    private static readonly string[] Admin = { NewsroomRole.SystemAdmin };
    private static readonly string[] Researcher = { NewsroomRole.Researcher };

    private static IDateTimeProvider Clock(DateTimeOffset now)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        return clock;
    }

    private static async Task<EditorialArticle> SeedAsync(TestApplicationDbContext ctx, bool published, string owner = "rep-1")
    {
        var article = EditorialArticle.Create("العنوان الأصلي", "الملخص", "النص الأصلي", owner, null, null);
        if (published)
        {
            article.Publish("pub-1", T0.AddHours(-1));
        }

        ctx.EditorialArticles.Add(article);
        await ctx.SaveChangesAsync(default);
        return article;
    }

    private static Task<EditorialArticleResult> Edit(
        TestApplicationDbContext ctx, Guid id, string title, string user, string[] roles, string? reason = null, DateTimeOffset? at = null)
        => new UpdateEditorialArticleCommandHandler(ctx, Clock(at ?? T0), PassThroughHtmlSanitizer.Instance, NullLogger<UpdateEditorialArticleCommandHandler>.Instance)
            .Handle(new UpdateEditorialArticleCommand(id, title, null, "نص معدّل " + title, null, null, user, roles, reason), default);

    private static Task<EditorialCorrectionResult> Correct(
        TestApplicationDbContext ctx, Guid id, string user, string[] roles,
        CorrectionKind kind = CorrectionKind.Correction, string note = "تصحيح", bool major = false, DateTimeOffset? at = null)
        => new IssueEditorialCorrectionCommandHandler(ctx, Clock(at ?? T0), NullLogger<IssueEditorialCorrectionCommandHandler>.Instance)
            .Handle(new IssueEditorialCorrectionCommand(id, kind, note, major, user, roles), default);

    // ---------------- revisions ----------------

    [Fact]
    public async Task Editing_a_published_article_should_store_the_replaced_version_as_revision_one()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, published: true);

        var result = await Edit(ctx, article.Id, "العنوان الجديد", "senior-1", Senior, "تحديث رقم");

        result.Outcome.Should().Be(EditorialOutcome.Success);
        result.Article!.Title.Should().Be("العنوان الجديد");
        var revision = await ctx.EditorialArticleRevisions.AsNoTracking().SingleAsync();
        revision.ArticleId.Should().Be(article.Id);
        revision.RevisionNumber.Should().Be(1);
        revision.Title.Should().Be("العنوان الأصلي");
        revision.Body.Should().Be("النص الأصلي");
        revision.EditedByUserId.Should().Be("senior-1");
        revision.EditorRoles.Should().Be(NewsroomRole.SeniorEditor);
        revision.Reason.Should().Be("تحديث رقم");
        revision.EditedAtUtc.Should().Be(T0);
    }

    [Fact]
    public async Task Editing_approved_content_should_return_it_to_review_and_append_an_automatic_transition()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = EditorialArticle.Create("معتمد", "ملخص", "النص الأصلي", "author", null, null);
        article.SubmitForReview();
        article.Approve();
        ctx.EditorialArticles.Add(article);
        await ctx.SaveChangesAsync(default);

        var result = await Edit(ctx, article.Id, "تعديل بعد الاعتماد", "senior-1", Senior);

        result.Outcome.Should().Be(EditorialOutcome.Success);
        result.Article!.Status.Should().Be(EditorialArticleStatus.InReview.ToString());
        var transition = await ctx.EditorialArticleTransitions.AsNoTracking().SingleAsync();
        transition.FromStatus.Should().Be(EditorialArticleStatus.Approved);
        transition.ToStatus.Should().Be(EditorialArticleStatus.InReview);
        transition.Reason.Should().Be("content edited after approval");
    }

    [Fact]
    public async Task Editing_published_content_without_a_reason_of_five_characters_should_write_nothing()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, published: true);

        var result = await Edit(ctx, article.Id, "تعديل بلا سبب", "senior-1", Senior, "قصير");

        result.Outcome.Should().Be(EditorialOutcome.InvalidEditReason);
        (await ctx.EditorialArticleRevisions.CountAsync()).Should().Be(0);
        (await ctx.EditorialArticles.AsNoTracking().SingleAsync()).Title.Should().Be("العنوان الأصلي");
    }

    [Fact]
    public async Task Each_further_edit_of_a_published_article_should_append_the_next_revision_number_and_keep_the_chain()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, published: true);

        await Edit(ctx, article.Id, "نسخة ب", "senior-1", Senior, "تعديل تحريري");
        await Edit(ctx, article.Id, "نسخة ج", "managing-1", Managing, "تحديث الوقائع");

        var revisions = await ctx.EditorialArticleRevisions.AsNoTracking().OrderBy(r => r.RevisionNumber).ToListAsync();
        revisions.Select(r => r.RevisionNumber).Should().Equal(1, 2);
        revisions.Select(r => r.Title).Should().Equal("العنوان الأصلي", "نسخة ب"); // each row is the version that was replaced
    }

    [Fact]
    public async Task Editing_a_draft_should_not_create_a_revision()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, published: false);

        (await Edit(ctx, article.Id, "مسودة معدّلة", "rep-1", Reporter)).Outcome.Should().Be(EditorialOutcome.Success);

        (await ctx.EditorialArticleRevisions.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task A_forbidden_edit_of_a_published_article_should_write_no_revision_and_change_nothing()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, published: true);

        (await Edit(ctx, article.Id, "اختراق", "copy-1", Copy)).Outcome.Should().Be(EditorialOutcome.Forbidden);

        (await ctx.EditorialArticleRevisions.AnyAsync()).Should().BeFalse();
        (await ctx.EditorialArticles.AsNoTracking().SingleAsync()).Title.Should().Be("العنوان الأصلي");
    }

    [Fact]
    public void Update_validator_should_reject_an_edit_reason_longer_than_the_limit()
    {
        var validator = new UpdateEditorialArticleCommandValidator();
        var command = new UpdateEditorialArticleCommand(
            Guid.NewGuid(), "t", null, "b", null, null, "u", Senior, new string('x', EditorialArticleRevision.ReasonMaxLength + 1));

        validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.EditReason);
    }

    // ---------------- corrections ----------------

    [Theory]
    [InlineData(CorrectionKind.Correction, true)]
    [InlineData(CorrectionKind.Notice, false)]
    public async Task Senior_up_roles_can_issue_a_correction_on_a_published_article(CorrectionKind kind, bool major)
    {
        foreach (var roles in new[] { Senior, Managing, Chief })
        {
            await using var ctx = TestApplicationDbContext.CreateNew();
            var article = await SeedAsync(ctx, published: true);

            var result = await Correct(ctx, article.Id, "editor-1", roles, kind, "ملاحظة المحرر", major);

            result.Outcome.Should().Be(EditorialOutcome.Success);
            result.Correction!.Kind.Should().Be(kind.ToString());
            result.Correction.IsMajor.Should().Be(major);
            var saved = await ctx.EditorialCorrections.AsNoTracking().SingleAsync();
            saved.ArticleId.Should().Be(article.Id);
            saved.IssuedByUserId.Should().Be("editor-1");
            saved.IssuerRoles.Should().Be(roles[0]);
        }
    }

    [Fact]
    public async Task Roles_below_senior_and_system_admin_should_be_forbidden_from_issuing_a_correction()
    {
        foreach (var roles in new[] { Reporter, Copy, Admin })
        {
            await using var ctx = TestApplicationDbContext.CreateNew();
            // Reporter owns the article so they can see it — the deny must come from the role rule.
            var article = await SeedAsync(ctx, published: true, owner: "rep-1");

            var result = await Correct(ctx, article.Id, "rep-1", roles);

            result.Outcome.Should().Be(EditorialOutcome.Forbidden, because: string.Join(",", roles));
            (await ctx.EditorialCorrections.AnyAsync()).Should().BeFalse();
        }
    }

    [Fact]
    public async Task A_role_without_cms_access_should_get_not_found_for_a_correction()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, published: true);

        (await Correct(ctx, article.Id, "res-1", Researcher)).Outcome.Should().Be(EditorialOutcome.NotFound);
    }

    [Fact]
    public async Task A_correction_on_an_unpublished_article_should_be_a_conflict_and_write_nothing()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var draft = await SeedAsync(ctx, published: false);

        (await Correct(ctx, draft.Id, "senior-1", Senior)).Outcome.Should().Be(EditorialOutcome.Conflict);

        (await ctx.EditorialCorrections.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task A_correction_on_an_unknown_article_should_be_not_found()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();

        (await Correct(ctx, Guid.NewGuid(), "senior-1", Senior)).Outcome.Should().Be(EditorialOutcome.NotFound);
    }

    [Fact]
    public void Correction_validator_should_require_a_note_a_defined_kind_and_limit_the_length()
    {
        var validator = new IssueEditorialCorrectionCommandValidator();
        var ok = new IssueEditorialCorrectionCommand(Guid.NewGuid(), CorrectionKind.Correction, "نص", false, "u", Senior);

        validator.TestValidate(ok).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(ok with { Kind = CorrectionKind.Notice })
            .ShouldHaveValidationErrorFor(x => x.Kind)
            .WithErrorMessage("Notice is a legacy value and cannot be issued for a new correction.");
        validator.TestValidate(ok with { Note = " " }).ShouldHaveValidationErrorFor(x => x.Note);
        validator.TestValidate(ok with { Note = new string('x', EditorialCorrection.NoteMaxLength + 1) }).ShouldHaveValidationErrorFor(x => x.Note);
        validator.TestValidate(ok with { Kind = (CorrectionKind)99 }).ShouldHaveValidationErrorFor(x => x.Kind);
    }

    // ---------------- public exposure ----------------

    [Fact]
    public async Task Public_detail_should_list_corrections_in_issue_order_without_issuer_data()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, published: true);
        await Correct(ctx, article.Id, "senior-1", Senior, CorrectionKind.Notice, "تنويه أول", false, T0);
        await Correct(ctx, article.Id, "chief-1", Chief, CorrectionKind.Correction, "تصحيح ثانٍ", true, T0.AddMinutes(5));

        var detail = (await new GetPublishedNewsByIdQueryHandler(ctx).Handle(new GetPublishedNewsByIdQuery(article.Id), default)).Article;

        detail!.Corrections.Select(c => c.Note).Should().Equal("تنويه أول", "تصحيح ثانٍ");
        detail.Corrections.Select(c => c.IsMajor).Should().Equal(false, true);
        detail.Corrections.Select(c => c.Kind).Should().Equal("Notice", "Correction");
        typeof(Jusoor.Application.Editorial.Contracts.PublicCorrectionDto).GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo("Kind", "Note", "IsMajor", "IssuedAtUtc"); // no issuer id / roles can leak
    }

    [Fact]
    public async Task Corrections_of_one_article_should_not_appear_on_another()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var a = await SeedAsync(ctx, published: true);
        var b = await SeedAsync(ctx, published: true);
        await Correct(ctx, a.Id, "senior-1", Senior);

        var detail = (await new GetPublishedNewsByIdQueryHandler(ctx).Handle(new GetPublishedNewsByIdQuery(b.Id), default)).Article;

        detail!.Corrections.Should().BeEmpty();
    }

    // ---------------- history queries ----------------

    private static Task<EditorialHistoryResult> History(TestApplicationDbContext ctx, Guid id, string user, string[] roles)
        => new GetEditorialArticleHistoryQueryHandler(ctx).Handle(new GetEditorialArticleHistoryQuery(id, user, roles), default);

    [Fact]
    public async Task History_should_combine_transitions_revisions_and_corrections_oldest_first()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, published: true);
        ctx.EditorialArticleTransitions.Add(EditorialArticleTransition.Create(
            article.Id, EditorialArticleStatus.Approved, EditorialArticleStatus.Published, "managing-1", Managing, null, T0.AddHours(-1)));
        await ctx.SaveChangesAsync(default);
        await Edit(ctx, article.Id, "نسخة ب", "senior-1", Senior, "سبب كافٍ", T0);
        await Correct(ctx, article.Id, "senior-1", Senior, CorrectionKind.Correction, "تصحيح", false, T0.AddMinutes(1));

        var result = await History(ctx, article.Id, "senior-1", Senior);

        result.Outcome.Should().Be(EditorialOutcome.Success);
        result.History!.Transitions.Should().ContainSingle().Which.ToStatus.Should().Be("Published");
        var revision = result.History.Revisions.Should().ContainSingle().Subject;
        revision.RevisionNumber.Should().Be(1);
        revision.Title.Should().Be("العنوان الأصلي");
        result.History.Corrections.Should().ContainSingle().Which.Note.Should().Be("تصحيح");
    }

    [Fact]
    public async Task History_should_be_visible_to_the_owning_reporter_but_look_like_not_found_to_another_reporter()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, published: false, owner: "rep-1");

        (await History(ctx, article.Id, "rep-1", Reporter)).Outcome.Should().Be(EditorialOutcome.Success);
        (await History(ctx, article.Id, "rep-2", Reporter)).Outcome.Should().Be(EditorialOutcome.NotFound);
        (await History(ctx, article.Id, "res-1", Researcher)).Outcome.Should().Be(EditorialOutcome.NotFound);
        (await History(ctx, Guid.NewGuid(), "senior-1", Senior)).Outcome.Should().Be(EditorialOutcome.NotFound);
    }

    [Fact]
    public async Task Revision_snapshot_should_return_the_full_replaced_content_and_respect_visibility()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedAsync(ctx, published: true, owner: "rep-1");
        await Edit(ctx, article.Id, "نسخة ب", "senior-1", Senior, "نسخة جديدة");
        var handler = new GetEditorialArticleRevisionQueryHandler(ctx);

        var found = await handler.Handle(new GetEditorialArticleRevisionQuery(article.Id, 1, "senior-1", Senior), default);
        var missingNumber = await handler.Handle(new GetEditorialArticleRevisionQuery(article.Id, 2, "senior-1", Senior), default);
        var hidden = await handler.Handle(new GetEditorialArticleRevisionQuery(article.Id, 1, "rep-2", Reporter), default);

        found.Outcome.Should().Be(EditorialOutcome.Success);
        found.Revision!.Body.Should().Be("النص الأصلي");
        found.Revision.Title.Should().Be("العنوان الأصلي");
        missingNumber.Outcome.Should().Be(EditorialOutcome.NotFound);
        hidden.Outcome.Should().Be(EditorialOutcome.NotFound);
    }

    // ---------------- Slice 20b: Retracted / Archived ----------------

    private static async Task<EditorialArticle> SeedLockedAsync(TestApplicationDbContext ctx, bool retracted)
    {
        var article = EditorialArticle.Create("العنوان الأصلي", "الملخص", "النص الأصلي", "rep-1", null, null);
        article.Publish("pub-1", T0.AddHours(-1));
        if (retracted)
        {
            article.Retract("chief-1", "تم سحب الخبر لعدم دقته", T0.AddMinutes(-5));
        }
        else
        {
            article.Archive("senior-1", T0.AddMinutes(-5));
        }

        ctx.EditorialArticles.Add(article);
        await ctx.SaveChangesAsync(default);
        return article;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Editing_a_retracted_or_archived_article_should_be_a_conflict_for_senior_up_and_change_nothing(bool retracted)
    {
        foreach (var roles in new[] { Senior, Managing, Chief })
        {
            await using var ctx = TestApplicationDbContext.CreateNew();
            var article = await SeedLockedAsync(ctx, retracted);

            var result = await Edit(ctx, article.Id, "تعديل ممنوع", "editor-1", roles);

            result.Outcome.Should().Be(EditorialOutcome.Conflict, because: string.Join(",", roles));
            var saved = await ctx.EditorialArticles.AsNoTracking().SingleAsync();
            saved.Title.Should().Be("العنوان الأصلي");
            (await ctx.EditorialArticleRevisions.AnyAsync()).Should().BeFalse();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Editing_a_retracted_or_archived_article_should_be_forbidden_for_roles_that_cannot_edit_it(bool retracted)
    {
        foreach (var roles in new[] { Copy, Admin })
        {
            await using var ctx = TestApplicationDbContext.CreateNew();
            var article = await SeedLockedAsync(ctx, retracted);

            var result = await Edit(ctx, article.Id, "تعديل ممنوع", "user-1", roles);

            result.Outcome.Should().Be(EditorialOutcome.Forbidden, because: string.Join(",", roles));
            (await ctx.EditorialArticles.AsNoTracking().SingleAsync()).Title.Should().Be("العنوان الأصلي");
        }
    }

    [Fact]
    public async Task A_correction_can_be_issued_on_an_archived_article()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedLockedAsync(ctx, retracted: false);

        var result = await Correct(ctx, article.Id, "senior-1", Senior);

        result.Outcome.Should().Be(EditorialOutcome.Success);
        (await ctx.EditorialCorrections.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A_correction_on_a_retracted_article_should_be_a_conflict_and_write_nothing()
    {
        await using var ctx = TestApplicationDbContext.CreateNew();
        var article = await SeedLockedAsync(ctx, retracted: true);

        (await Correct(ctx, article.Id, "chief-1", Chief)).Outcome.Should().Be(EditorialOutcome.Conflict);

        (await ctx.EditorialCorrections.AnyAsync()).Should().BeFalse();
    }
}
