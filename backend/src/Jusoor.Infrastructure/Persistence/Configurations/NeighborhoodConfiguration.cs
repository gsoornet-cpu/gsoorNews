using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jusoor.Infrastructure.Persistence.Configurations;

public class NeighborhoodConfiguration : IEntityTypeConfiguration<Neighborhood>
{
    public void Configure(EntityTypeBuilder<Neighborhood> builder)
    {
        builder.ToTable("Neighborhoods");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.NameAr).IsRequired().HasMaxLength(100);
        builder.Property(n => n.NameEn).IsRequired().HasMaxLength(100);
        builder.Property(n => n.Slug).IsRequired().HasMaxLength(100);
        builder.Property(n => n.Latitude).HasPrecision(9, 6);
        builder.Property(n => n.Longitude).HasPrecision(9, 6);

        // Same reasoning as City's own (CountryId, Slug) index, one tier
        // down — a neighborhood slug is only guaranteed unique within its
        // own City.
        builder.HasIndex(n => new { n.CityId, n.Slug }).IsUnique();

        // Unlike City.RegionId, CityId here is required (BaseEntity's
        // `required Guid CityId`) — a modeled Neighborhood always belongs
        // to exactly one City; it's whether a City has ANY Neighborhood
        // rows at all that's optional (see Neighborhood.cs).
        builder.HasOne(n => n.City)
            .WithMany(c => c.Neighborhoods)
            .HasForeignKey(n => n.CityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
