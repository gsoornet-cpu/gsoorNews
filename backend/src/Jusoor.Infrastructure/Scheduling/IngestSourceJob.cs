using Hangfire;
using Jusoor.Application.Ingestion;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Jusoor.Infrastructure.Scheduling;

/// <summary>
/// Wraps a single IngestSourceCommand as its own Hangfire job. Deliberately
/// one job per Source (enqueued individually by SourceIngestionSweepJob)
/// rather than one big job looping over every Source serially — isolates
/// failures (a Hangfire job that throws gets Hangfire's own retry/backoff,
/// scoped to that one Source) and lets Hangfire run them in parallel across
/// worker threads instead of one Source's slow fetch blocking the rest.
/// </summary>
public class IngestSourceJob
{
    private readonly ISender _sender;
    private readonly ILogger<IngestSourceJob> _logger;

    public IngestSourceJob(ISender sender, ILogger<IngestSourceJob> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    /// <summary>
    /// IJobCancellationToken (not a plain CancellationToken) is Hangfire's
    /// own mechanism for cooperative cancellation on shutdown/deletion —
    /// Hangfire substitutes a real token at execution time regardless of
    /// what's written in the enqueue-time expression, which is why callers
    /// pass the placeholder JobCancellationToken.Null when enqueuing.
    /// </summary>
    public async Task RunAsync(Guid sourceId, IJobCancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // IngestSourceCommand already catches and classifies its own
        // fetch/parse failures internally (see SourceFetchLog) rather than
        // throwing for expected failure modes — an exception escaping here
        // means something genuinely unexpected happened (e.g. a DB outage),
        // which is exactly the case Hangfire's own retry policy should
        // handle, not something to swallow.
        var result = await _sender.Send(new IngestSourceCommand(sourceId), CancellationToken.None);

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Scheduled ingestion for source {SourceId} did not succeed: {Outcome} — {Error}",
                sourceId, result.Outcome, result.ErrorSummary);
        }
    }
}
