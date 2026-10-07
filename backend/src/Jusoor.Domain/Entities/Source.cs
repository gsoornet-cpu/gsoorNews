using Jusoor.Domain.Common;
using Jusoor.Domain.Enums;

namespace Jusoor.Domain.Entities;

/// <summary>
/// Per spec §05: every source is stored with its base layer and a
/// ReliabilityScore that updates dynamically as items pass through it. Phase 0
/// creates the table; Phase 1's source workers are what actually write to it
/// and update the score. Nothing here should be read as "the relevance engine
/// is implemented" — it is not.
/// </summary>
public class Source : AuditableEntity
{
    public required string Name { get; set; }
    public required SourceLayer Layer { get; set; }
    public string? FeedUrl { get; set; }       // RSS/API endpoint, when one exists
    public string? OfficialWebsiteUrl { get; set; }

    /// <summary>
    /// 0.0–1.0. Starts at a conservative default and is adjusted by the
    /// relevance/dedup pipeline in later phases — never edited by hand in
    /// production once real data starts flowing.
    /// </summary>
    public decimal ReliabilityScore { get; set; } = 0.5m;

    public bool IsActive { get; set; } = true;
}
