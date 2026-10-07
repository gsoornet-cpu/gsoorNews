using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jusoor.Infrastructure.Persistence.Configurations;

public class DiasporaProfileConfiguration : IEntityTypeConfiguration<DiasporaProfile>
{
    public void Configure(EntityTypeBuilder<DiasporaProfile> builder)
    {
        builder.ToTable("DiasporaProfiles");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.UserId).IsRequired();
        // One profile per user — enforced at the database, not just by
        // "always query then create" application discipline.
        builder.HasIndex(p => p.UserId).IsUnique();

        // No FK-with-navigation onto ApplicationUser (Infrastructure/Identity)
        // — same reasoning as HumanReviewDecision.ReviewedByUserId: nothing
        // else in this codebase FKs a Domain entity onto Identity's user
        // table directly.
        //
        // Country/Region/City ARE real Domain entities, so these DO get
        // proper FKs — but no navigation properties on DiasporaProfile
        // itself (it only ever needs the ids; a query that wants the
        // resolved names loads them the same explicit-dictionary way
        // Stories/Review queries already do, not via .Include()).
        builder.HasOne<Country>().WithMany().HasForeignKey(p => p.CountryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Region>().WithMany().HasForeignKey(p => p.RegionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<City>().WithMany().HasForeignKey(p => p.CityId).OnDelete(DeleteBehavior.Restrict);
    }
}
