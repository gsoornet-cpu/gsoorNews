using Hangfire;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Jusoor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jusoor.Infrastructure.Scheduling;

/// <summary>
/// Publishes due, approved newsroom articles. The article row is the durable
/// schedule; each transition and its audit record are saved together. Status
/// is a concurrency token, so overlapping workers and a concurrent cancel can
/// never publish twice or overwrite the winning state transition.
/// </summary>
public sealed class ScheduledEditorialPublicationJob
{
    public const string JobId = "scheduled-editorial-publications";
    public const string SystemActorId = "system:scheduled-publisher";
    private static readonly string[] SystemActorRoles = ["ScheduledPublisher"];
    private const int BatchSize = 100;

    private readonly ApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<ScheduledEditorialPublicationJob> _logger;

    public ScheduledEditorialPublicationJob(
        ApplicationDbContext context,
        IDateTimeProvider clock,
        ILogger<ScheduledEditorialPublicationJob> logger)
        => (_context, _clock, _logger) = (context, clock, logger);

    public async Task RunAsync(IJobCancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = _clock.UtcNow.ToUniversalTime();
        var ids = await _context.EditorialArticles.AsNoTracking()
            .Where(a => a.Status == EditorialArticleStatus.Scheduled && a.ScheduledPublishAtUtc <= now)
            .OrderBy(a => a.ScheduledPublishAtUtc)
            .ThenBy(a => a.Id)
            .Select(a => a.Id)
            .Take(BatchSize)
            .ToListAsync();

        var published = 0;
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var article = await _context.EditorialArticles
                .FirstOrDefaultAsync(a => a.Id == id
                    && a.Status == EditorialArticleStatus.Scheduled
                    && a.ScheduledPublishAtUtc <= now);
            if (article is null) continue;

            var from = article.Status;
            article.PublishScheduled(SystemActorId, now);
            var transition = EditorialArticleTransition.Create(
                article.Id, from, article.Status, SystemActorId, SystemActorRoles,
                "Automated scheduled publication.", now);
            _context.EditorialArticleTransitions.Add(transition);

            try
            {
                await _context.SaveChangesAsync();
                published++;
            }
            catch (DbUpdateConcurrencyException)
            {
                // A cancellation or another worker committed first. The row's
                // status token prevents stale writes; the failed SaveChanges
                // transaction also rolls back the audit row.
                _context.Entry(article).State = EntityState.Detached;
                _context.Entry(transition).State = EntityState.Detached;
                _logger.LogInformation("Scheduled publication for article {ArticleId} was superseded by another workflow action.", id);
            }
        }

        _logger.LogInformation("Scheduled publication sweep completed: {PublishedCount} article(s) published from {DueCount} due article(s).", published, ids.Count);
    }
}
