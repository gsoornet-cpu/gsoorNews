using Jusoor.Domain.Common;
using Jusoor.Domain.Enums;

namespace Jusoor.Domain.Entities;

/// <summary>
/// One record per source-fetch attempt. This is what "source health
/// information" (§7) and ingestion observability (§20) actually mean in
/// practice — Source.ReliabilityScore (Phase 0) reflects content trust over
/// time; this reflects operational health (is this feed reachable, is it
/// timing out, is it well-formed) on a per-run basis. Different concerns,
/// deliberately separate fields.
/// </summary>
public class SourceFetchLog : BaseEntity
{
    public Guid SourceId { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public SourceFetchOutcome Outcome { get; private set; }
    public int? HttpStatusCode { get; private set; }

    public int ItemsFound { get; private set; }
    public int ItemsIngested { get; private set; }
    public int ItemsSkippedAsDuplicate { get; private set; }

    /// <summary>Safe-for-logs summary only — never the raw exception/stack
    /// trace (that goes to structured application logs via ILogger, keyed
    /// by this row's Id as correlation), and never any fetched content.</summary>
    public string? ErrorSummary { get; private set; }

    private SourceFetchLog() { }

    public static SourceFetchLog Start(Guid sourceId, DateTimeOffset nowUtc) => new()
    {
        SourceId = sourceId,
        StartedAtUtc = nowUtc,
        Outcome = SourceFetchOutcome.Success // provisional; Complete(...) sets the real outcome
    };

    public void Complete(
        SourceFetchOutcome outcome,
        DateTimeOffset nowUtc,
        int itemsFound = 0,
        int itemsIngested = 0,
        int itemsSkippedAsDuplicate = 0,
        int? httpStatusCode = null,
        string? errorSummary = null)
    {
        CompletedAtUtc = nowUtc;
        Outcome = outcome;
        ItemsFound = itemsFound;
        ItemsIngested = itemsIngested;
        ItemsSkippedAsDuplicate = itemsSkippedAsDuplicate;
        HttpStatusCode = httpStatusCode;
        ErrorSummary = errorSummary;
    }
}
