using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jusoor.Infrastructure.Persistence.Configurations;

public class MyPlaceConfiguration : IEntityTypeConfiguration<MyPlace>
{
    public void Configure(EntityTypeBuilder<MyPlace> builder)
    {
        builder.ToTable("MyPlaces");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.UserId).IsRequired();

        // Can't follow the same city twice — enforced at the database.
        builder.HasIndex(p => new { p.UserId, p.CityId }).IsUnique();

        builder.HasOne<City>().WithMany().HasForeignKey(p => p.CityId).OnDelete(DeleteBehavior.Restrict);
    }
}
