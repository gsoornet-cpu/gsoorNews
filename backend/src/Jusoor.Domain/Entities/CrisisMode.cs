using Jusoor.Domain.Common;

namespace Jusoor.Domain.Entities;

/// <summary>
/// The newsroom-controlled public crisis banner/centre. It contains editorial
/// messaging and affected country IDs only; it is deliberately independent of
/// private Atmaen case data and never publishes case counts.
/// </summary>
public sealed class CrisisMode : AuditableEntity
{
    public const string SingletonKey = "global";
    public const int TitleMaxLength = 200;
    public const int SummaryMaxLength = 2000;
    public const int SourceNameMaxLength = 160;
    public const int SourceUrlMaxLength = 2048;

    public string Key { get; private set; } = SingletonKey;
    public bool IsActive { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string SourceName { get; private set; } = string.Empty;
    public string SourceUrl { get; private set; } = string.Empty;
    public string Severity { get; private set; } = "urgent";
    public string AffectedCountryIdsJson { get; private set; } = "[]";

    private CrisisMode() { }

    public static CrisisMode Create(bool isActive, string title, string summary, string sourceName, string sourceUrl, string severity, string affectedCountryIdsJson)
    {
        var mode = new CrisisMode();
        mode.Update(isActive, title, summary, sourceName, sourceUrl, severity, affectedCountryIdsJson);
        return mode;
    }

    public void Update(bool isActive, string title, string summary, string sourceName, string sourceUrl, string severity, string affectedCountryIdsJson)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > TitleMaxLength)
            throw new ArgumentException("A crisis title is required and must be 200 characters or fewer.", nameof(title));
        if (string.IsNullOrWhiteSpace(summary) || summary.Trim().Length > SummaryMaxLength)
            throw new ArgumentException("A crisis summary is required and must be 2,000 characters or fewer.", nameof(summary));
        if (severity is not ("advisory" or "urgent" or "emergency"))
            throw new ArgumentException("Unsupported crisis severity.", nameof(severity));
        if (isActive && (string.IsNullOrWhiteSpace(sourceName) || sourceName.Trim().Length > SourceNameMaxLength))
            throw new ArgumentException("An active crisis requires a source name.", nameof(sourceName));
        if (!string.IsNullOrWhiteSpace(sourceUrl) && (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var sourceUri) || sourceUri.Scheme is not ("https" or "http") || sourceUrl.Length > SourceUrlMaxLength))
            throw new ArgumentException("Source URL must use HTTP or HTTPS.", nameof(sourceUrl));
        if (isActive && string.IsNullOrWhiteSpace(sourceUrl))
            throw new ArgumentException("An active crisis requires a source URL.", nameof(sourceUrl));
        if (string.IsNullOrWhiteSpace(affectedCountryIdsJson) || affectedCountryIdsJson.Length > 20_000)
            throw new ArgumentException("Affected countries must be provided as a bounded JSON array.", nameof(affectedCountryIdsJson));

        Key = SingletonKey;
        IsActive = isActive;
        Title = title.Trim();
        Summary = summary.Trim();
        SourceName = sourceName?.Trim() ?? string.Empty;
        SourceUrl = string.IsNullOrWhiteSpace(sourceUrl) ? string.Empty : new Uri(sourceUrl).ToString();
        Severity = severity;
        AffectedCountryIdsJson = affectedCountryIdsJson;
    }
}
