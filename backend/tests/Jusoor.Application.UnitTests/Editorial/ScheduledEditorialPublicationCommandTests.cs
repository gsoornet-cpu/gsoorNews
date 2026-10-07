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

public class ScheduledEditorialPublicationCommandTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] Senior = [NewsroomRole.SeniorEditor];

    private static IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        return clock;
    }

    private static async Task<EditorialArticle> SeedApprovedAsync(TestApplicationDbContext context)
    {
        var article = EditorialArticle.Create("عنوان", "ملخص", "النص", "reporter-1", null, null);
        article.SubmitForReview();
        article.Approve();
        context.EditorialArticles.Add(article);
        await context.SaveChangesAsync();
        return article;
    }

    [Fact]
    public async Task Schedule_should_persist_future_time_and_append_an_audit_transition()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var article = await SeedApprovedAsync(context);
        var handler = new ScheduleEditorialPublicationCommandHandler(
            context, Clock(), NullLogger<ScheduleEditorialPublicationCommandHandler>.Instance);

        var result = await handler.Handle(new ScheduleEditorialPublicationCommand(
            article.Id, Now.AddHours(2), "senior-1", Senior, "Embargo until publication time."), default);

        result.Outcome.Should().Be(EditorialOutcome.Success);
        var saved = await context.EditorialArticles.AsNoTracking().SingleAsync();
        saved.Status.Should().Be(EditorialArticleStatus.Scheduled);
        saved.ScheduledPublishAtUtc.Should().Be(Now.AddHours(2));
        var transition = await context.EditorialArticleTransitions.AsNoTracking().SingleAsync();
        transition.FromStatus.Should().Be(EditorialArticleStatus.Approved);
        transition.ToStatus.Should().Be(EditorialArticleStatus.Scheduled);
        transition.ActorUserId.Should().Be("senior-1");
        transition.Reason.Should().Be("Embargo until publication time.");
    }

    [Fact]
    public async Task Schedule_should_reject_a_non_future_time_without_changing_article_or_audit()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var article = await SeedApprovedAsync(context);
        var handler = new ScheduleEditorialPublicationCommandHandler(
            context, Clock(), NullLogger<ScheduleEditorialPublicationCommandHandler>.Instance);

        var result = await handler.Handle(new ScheduleEditorialPublicationCommand(
            article.Id, Now, "senior-1", Senior), default);

        result.Outcome.Should().Be(EditorialOutcome.InvalidScheduleTime);
        (await context.EditorialArticles.AsNoTracking().SingleAsync()).Status.Should().Be(EditorialArticleStatus.Approved);
        (await context.EditorialArticleTransitions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Scheduling_should_require_senior_editorial_role_and_an_approved_state()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var approved = await SeedApprovedAsync(context);
        var draft = EditorialArticle.Create("مسودة", null, "النص", "reporter-2", null, null);
        context.EditorialArticles.Add(draft);
        await context.SaveChangesAsync();
        var handler = new ScheduleEditorialPublicationCommandHandler(
            context, Clock(), NullLogger<ScheduleEditorialPublicationCommandHandler>.Instance);

        var admin = await handler.Handle(new ScheduleEditorialPublicationCommand(
            approved.Id, Now.AddHours(1), "admin-1", [NewsroomRole.SystemAdmin]), default);
        var reporter = await handler.Handle(new ScheduleEditorialPublicationCommand(
            approved.Id, Now.AddHours(1), "reporter-1", [NewsroomRole.Reporter]), default);
        var unapproved = await handler.Handle(new ScheduleEditorialPublicationCommand(
            draft.Id, Now.AddHours(1), "senior-1", Senior), default);

        admin.Outcome.Should().Be(EditorialOutcome.Forbidden);
        reporter.Outcome.Should().Be(EditorialOutcome.Forbidden);
        unapproved.Outcome.Should().Be(EditorialOutcome.Conflict);
        (await context.EditorialArticleTransitions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Cancellation_should_clear_schedule_return_to_approved_and_be_audited()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var article = await SeedApprovedAsync(context);
        article.SchedulePublication(Now.AddHours(1), Now);
        context.EditorialArticleTransitions.Add(EditorialArticleTransition.Create(
            article.Id,
            EditorialArticleStatus.Approved,
            EditorialArticleStatus.Scheduled,
            "senior-1",
            Senior,
            null,
            Now));
        await context.SaveChangesAsync();
        var handler = new CancelScheduledEditorialPublicationCommandHandler(
            context, Clock(), NullLogger<CancelScheduledEditorialPublicationCommandHandler>.Instance);

        var result = await handler.Handle(new CancelScheduledEditorialPublicationCommand(
            article.Id, "senior-1", Senior), default);

        result.Outcome.Should().Be(EditorialOutcome.Success);
        var saved = await context.EditorialArticles.AsNoTracking().SingleAsync();
        saved.Status.Should().Be(EditorialArticleStatus.Approved);
        saved.ScheduledPublishAtUtc.Should().BeNull();
        (await context.EditorialArticleTransitions.CountAsync()).Should().Be(2);
    }
}
