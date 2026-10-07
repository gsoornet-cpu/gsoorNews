using Jusoor.Domain.Common;

namespace Jusoor.Domain.Entities;

public class City : BaseEntity
{
    public required Guid CountryId { get; set; }
    public Country? Country { get; set; }

    // Nullable, unlike CountryId: a City's Region is Phase 2's addition
    // onto data that may already exist without one (same "don't fake it,
    // populate as real data arrives" reasoning as Latitude/Longitude
    // below) — a City is never required to have a Region to function, only
    // gains extra precision when it does.
    public Guid? RegionId { get; set; }
    public Region? Region { get; set; }

    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public required string Slug { get; set; } // used in /{country}/{city}/ routes

    // Nullable on purpose: not every source has precise coordinates for a
    // city. Populated as real geo data arrives in Phase 1/2, not faked here.
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public ICollection<Neighborhood> Neighborhoods { get; set; } = new List<Neighborhood>();
}
