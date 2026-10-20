using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jusoor.Infrastructure.Persistence.Configurations;

public sealed class DiasporaEstimateConfiguration : IEntityTypeConfiguration<DiasporaEstimate>
{
    public void Configure(EntityTypeBuilder<DiasporaEstimate> builder)
    {
        builder.ToTable("DiasporaEstimates");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.EstimatedPopulation).IsRequired();
        builder.Property(item => item.SourceName).IsRequired().HasMaxLength(200);
        builder.Property(item => item.SourceUrl).IsRequired().HasMaxLength(2048);
        builder.Property(item => item.SourcePublishedOn).IsRequired();
        builder.HasOne<Country>().WithMany().HasForeignKey(item => item.CountryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<City>().WithMany().HasForeignKey(item => item.CityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.CountryId, item.CityId }).IsUnique();
        builder.HasIndex(item => item.IsPublished);
    }
}
