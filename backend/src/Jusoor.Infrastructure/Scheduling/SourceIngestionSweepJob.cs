using Hangfire;
using Jusoor.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jusoor.Infrastructure.Scheduling;

/// <summary>
/// The single recurring job registered with Hangfire (see Program.cs). Its
/// only job is to look up currently-active Sources and enqueue one
/// IngestSourceJob per Source — it does no fetching itself, so it finishes
/// almost instantly regardless of how slow any individual source is.
/// </summary>
public class SourceIngestionSweepJob
{
    private readonly IApplicationDbContext _context;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<SourceIngestionSweepJob> _logger;

    public SourceIngestionSweepJob(
        IApplicationDbContext context,
        IBackgroundJobClient backgroundJobClient,
        ILogger<SourceIngestionSweepJob> logger)
    {
        _context = context;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    public async Task RunAsync(IJobCancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var activeSourceIds = await _context.Sources
            .Where(s => s.IsActive)
            .Select(s => s.Id)
            .ToListAsync();

        foreach (var sourceId in activeSourceIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _backgroundJobClient.Enqueue<IngestSourceJob>(
                job => job.RunAsync(sourceId, JobCancellationToken.Null));
        }

        _logger.LogInformation("Source ingestion sweep enqueued {Count} source(s).", activeSourceIds.Count);
    }
}
