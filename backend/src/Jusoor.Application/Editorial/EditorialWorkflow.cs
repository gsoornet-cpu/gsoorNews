using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jusoor.Application.Editorial;

/// <summary>
/// Applies one workflow transition end-to-end: load → visibility (NotFound) →
/// role/ownership (Forbidden) → state (Conflict) → mutate → append the audit
/// row → save, atomically (one SaveChanges persists both the article and its
/// transition record). Shared by every transition handler so the checks and
/// the audit trail can never drift apart. <paramref name="publicNotice"/> is
/// used by Retract only (Slice 20b); the internal reason always travels in
/// <paramref name="reason"/> and ends up on the transition row, never on the article.
/// </summary>
internal static class EditorialWorkflow
{
    public static async Task<EditorialArticleResult> ApplyAsync(
        IApplicationDbContext context,
        IDateTimeProvider clock,
        ILogger logger,
        EditorialAction action,
        Guid articleId,
        string actorUserId,
        IReadOnlyList<string> actorRoles,
        string? reason,
        CancellationToken cancellationToken,
        string? publicNotice = null,
        DateTimeOffset? scheduledPublishAtUtc = null)
    {
        var article = await context.EditorialArticles.FirstOrDefaultAsync(a => a.Id == articleId, cancellationToken);

        // Not-visible and not-existing are the same answer (no id enumeration).
        if (article is null || !EditorialArticleAccess.CanView(actorRoles, actorUserId, article))
        {
            return new EditorialArticleResult(EditorialOutcome.NotFound, null);
        }

        switch (EditorialArticleAccess.CheckTransition(action, actorRoles, actorUserId, article))
        {
            case TransitionCheck.Forbidden:
                return new EditorialArticleResult(EditorialOutcome.Forbidden, null);
            case TransitionCheck.InvalidState:
                return new EditorialArticleResult(EditorialOutcome.Conflict, EditorialSupport.ToDto(article));
        }

        var from = article.Status;
        var now = clock.UtcNow;
        if (action == EditorialAction.SchedulePublication
            && scheduledPublishAtUtc is { } requestedPublishAtUtc
            && requestedPublishAtUtc <= now)
        {
            return new EditorialArticleResult(EditorialOutcome.InvalidScheduleTime, EditorialSupport.ToDto(article));
        }

        switch (action)
        {
            case EditorialAction.Submit: article.SubmitForReview(); break;
            case EditorialAction.RequestRevision: article.RequestRevision(); break;
            case EditorialAction.Approve: article.Approve(); break;
            case EditorialAction.Publish: article.Publish(actorUserId, now); break;
            case EditorialAction.Unpublish: article.Unpublish(); break;
            case EditorialAction.SchedulePublication:
                if (scheduledPublishAtUtc is null)
                    throw new ArgumentException("A scheduled publication time is required.", nameof(scheduledPublishAtUtc));
                article.SchedulePublication(scheduledPublishAtUtc.Value, now);
                break;
            case EditorialAction.CancelScheduledPublication: article.CancelScheduledPublication(); break;
            // Slice 20b (Q9). The public notice is only meaningful for Retract; the
            // validator guarantees it is present, and the domain re-checks it.
            case EditorialAction.Retract: article.Retract(actorUserId, publicNotice ?? string.Empty, now); break;
            case EditorialAction.Archive: article.Archive(actorUserId, now); break;
            case EditorialAction.RestoreArchived: article.RestoreFromArchive(); break;
            case EditorialAction.ReinstateRetracted: article.ReinstateRetracted(); break;
        }

        context.EditorialArticleTransitions.Add(
            EditorialArticleTransition.Create(article.Id, from, article.Status, actorUserId, actorRoles, reason, now));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The workflow state changed after authorization (for example, the
            // publication worker won a race with a cancellation request).
            var current = await context.EditorialArticles.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == articleId, cancellationToken);
            return current is null || !EditorialArticleAccess.CanView(actorRoles, actorUserId, current)
                ? new EditorialArticleResult(EditorialOutcome.NotFound, null)
                : new EditorialArticleResult(EditorialOutcome.Conflict, EditorialSupport.ToDto(current));
        }

        logger.LogInformation(
            "EditorialArticle {ArticleId} {Action}: {From} -> {To} by {UserId}",
            article.Id, action, from, article.Status, actorUserId);

        return new EditorialArticleResult(EditorialOutcome.Success, EditorialSupport.ToDto(article));
    }
}
