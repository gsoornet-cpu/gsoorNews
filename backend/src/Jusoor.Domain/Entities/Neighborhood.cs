using Jusoor.Domain.Common;

namespace Jusoor.Domain.Entities;

/// <summary>
/// Fourth, explicitly optional tier of the geo hierarchy (spec §09:
/// "الحي (اختياري)"). Unlike Region — which the spec treats as a normal,
/// expected part of the hierarchy — most Cities will legitimately never
/// have any Neighborhood rows, and that's correct, not incomplete data:
/// the spec only calls for neighborhood-level precision where it
/// meaningfully sharpens the Diaspora Map or an Atmaen case, not as a
/// blanket requirement.
/// </summary>
public class Neighborhood : BaseEntity
{
    public required Guid CityId { get; set; }
    public City? City { get; set; }

    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public required string Slug { get; set; } // used in /{country}/{city}/{neighborhood}/ routes

    // Same "nullable until real data exists" reasoning as City's own
    // Latitude/Longitude.
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}
