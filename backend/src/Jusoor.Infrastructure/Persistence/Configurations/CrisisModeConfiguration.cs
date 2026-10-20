using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jusoor.Infrastructure.Persistence.Configurations;

public sealed class CrisisModeConfiguration : IEntityTypeConfiguration<CrisisMode>
{
    public void Configure(EntityTypeBuilder<CrisisMode> builder)
    {
        builder.ToTable("CrisisModes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).IsRequired().HasMaxLength(16);
        builder.HasIndex(x => x.Key).IsUnique();
        builder.Property(x => x.Title).IsRequired().HasMaxLength(CrisisMode.TitleMaxLength);
        builder.Property(x => x.Summary).IsRequired().HasMaxLength(CrisisMode.SummaryMaxLength);
        builder.Property(x => x.SourceName).IsRequired().HasMaxLength(CrisisMode.SourceNameMaxLength);
        builder.Property(x => x.SourceUrl).IsRequired().HasMaxLength(CrisisMode.SourceUrlMaxLength);
        builder.Property(x => x.Severity).IsRequired().HasMaxLength(16);
        builder.Property(x => x.AffectedCountryIdsJson).IsRequired().HasMaxLength(20_000);
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450);
        builder.Property(x => x.LastModifiedByUserId).HasMaxLength(450);
    }
}
