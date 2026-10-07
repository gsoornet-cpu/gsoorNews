using Jusoor.Domain.Common;

namespace Jusoor.Domain.Entities;

/// <summary>
/// Top level of the geo hierarchy required by spec §09 (Country → Region →
/// City → Neighborhood). Only Country and City were modeled in Phase 0 —
/// Region and Neighborhood are Phase 2's addition (see Region.cs and
/// Neighborhood.cs), added alongside the Atmaen work that actually needs
/// the fuller hierarchy; modeling them earlier with no consumer would have
/// been exactly the "tables because the frontend might need them"
/// anti-pattern the assessment calls out.
/// </summary>
public class Country : BaseEntity
{
    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public required string IsoCode2 { get; set; } // e.g. "AE", "US", "GB"
    public required string Slug { get; set; }      // used in /{country}/ routes

    public ICollection<City> Cities { get; set; } = new List<City>();
    public ICollection<Region> Regions { get; set; } = new List<Region>();
}
