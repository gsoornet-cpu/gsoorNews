namespace Jusoor.Infrastructure.Ingestion;

public class IngestionOptions
{
    public const string SectionName = "Ingestion";

    public int ConnectTimeoutSeconds { get; set; } = 10;
    public int ReadTimeoutSeconds { get; set; } = 20;
    public int MaxResponseBytes { get; set; } = 10 * 1024 * 1024; // 10 MB — generous for an RSS/XML feed, far below a DoS-scale response
    public int MaxRedirects { get; set; } = 3;

    /// <summary>How often the sweep job checks all active Sources and
    /// enqueues an ingestion run for each. A single interval for every
    /// Source is a deliberate simplification for this slice — per-Source
    /// intervals (some feeds update every minute, some once a day) are a
    /// real future improvement, not built here to avoid adding an unused
    /// per-Source scheduling field before anything reads it.</summary>
    public int ScheduleIntervalMinutes { get; set; } = 15;
}
