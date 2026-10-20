using Jusoor.Domain.Common;

namespace Jusoor.Domain.Entities;

/// <summary>Curated diaspora estimate with mandatory provenance, kept separate from privacy-thresholded member signals.</summary>
public sealed class DiasporaEstimate : AuditableEntity
{
    public Guid CountryId { get; private set; }
    public Guid? CityId { get; private set; }
    public int EstimatedPopulation { get; private set; }
    public string SourceName { get; private set; } = null!;
    public string SourceUrl { get; private set; } = null!;
    public DateOnly SourcePublishedOn { get; private set; }
    public bool IsPublished { get; private set; }

    private DiasporaEstimate() { }

    public static DiasporaEstimate Create(Guid countryId, Guid? cityId, int estimatedPopulation, string sourceName, string sourceUrl, DateOnly sourcePublishedOn, bool isPublished)
    {
        var estimate = new DiasporaEstimate();
        estimate.Update(countryId, cityId, estimatedPopulation, sourceName, sourceUrl, sourcePublishedOn, isPublished);
        return estimate;
    }

    public void Update(Guid countryId, Guid? cityId, int estimatedPopulation, string sourceName, string sourceUrl, DateOnly sourcePublishedOn, bool isPublished)
    {
        if (countryId == Guid.Empty) throw new ArgumentException("A country is required.", nameof(countryId));
        if (estimatedPopulation <= 0) throw new ArgumentOutOfRangeException(nameof(estimatedPopulation));
        if (string.IsNullOrWhiteSpace(sourceName) || sourceName.Trim().Length > 200) throw new ArgumentException("A source name of 1–200 characters is required.", nameof(sourceName));
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http") || sourceUrl.Length > 2048) throw new ArgumentException("The source URL must be an HTTP(S) URL of at most 2048 characters.", nameof(sourceUrl));
        if (sourcePublishedOn > DateOnly.FromDateTime(DateTime.UtcNow)) throw new ArgumentException("The source publication date cannot be in the future.", nameof(sourcePublishedOn));
        CountryId = countryId;
        CityId = cityId;
        EstimatedPopulation = estimatedPopulation;
        SourceName = sourceName.Trim();
        SourceUrl = uri.ToString();
        SourcePublishedOn = sourcePublishedOn;
        IsPublished = isPublished;
    }
}
