using Jusoor.Domain.Common;

namespace Jusoor.Domain.Entities;

/// <summary>
/// Second tier of the geo hierarchy required by spec §09 (Country → Region
/// → City → Neighborhood — "المنطقة/الولاية" in the spec, i.e. a state,
/// province, or governorate). Deferred from Phase 0 to here exactly as
/// Country.cs's own comment said it would be: Country and City existed
/// without it because nothing consumed it yet; Atmaen (spec §10 step 2 —
/// "اختيار المنطقة/المدينة") and the fuller Diaspora Map (§09) are the
/// first real consumers.
/// </summary>
public class Region : BaseEntity
{
    public required Guid CountryId { get; set; }
    public Country? Country { get; set; }

    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public required string Slug { get; set; } // used in /{country}/{region}/ routes

    public ICollection<City> Cities { get; set; } = new List<City>();
}
