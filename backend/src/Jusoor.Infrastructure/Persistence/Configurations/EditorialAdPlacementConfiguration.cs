using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jusoor.Infrastructure.Persistence.Configurations;

public sealed class EditorialAdPlacementConfiguration : IEntityTypeConfiguration<EditorialAdPlacement>
{
    public void Configure(EntityTypeBuilder<EditorialAdPlacement> builder)
    {
        builder.ToTable("EditorialAdPlacements");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SlotKey).IsRequired().HasMaxLength(EditorialAdPlacement.SlotKeyMaxLength);
        builder.Property(x => x.Surface).IsRequired().HasConversion<int>();
        builder.Property(x => x.TargetUrl).IsRequired().HasMaxLength(EditorialAdPlacement.TargetUrlMaxLength);
        builder.Property(x => x.AltText).IsRequired().HasMaxLength(EditorialAdPlacement.AltTextMaxLength);
        builder.HasIndex(x => new { x.SlotKey, x.Surface }).IsUnique();
        builder.HasIndex(x => x.MediaAssetId);
        builder.HasOne(x => x.MediaAsset).WithMany().HasForeignKey(x => x.MediaAssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
