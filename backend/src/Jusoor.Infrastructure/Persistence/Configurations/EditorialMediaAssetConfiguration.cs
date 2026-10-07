using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jusoor.Infrastructure.Persistence.Configurations;

public sealed class EditorialMediaAssetConfiguration : IEntityTypeConfiguration<EditorialMediaAsset>
{
    public void Configure(EntityTypeBuilder<EditorialMediaAsset> builder)
    {
        builder.ToTable("EditorialMediaAssets", table =>
        {
            table.HasCheckConstraint("CK_EditorialMediaAssets_SizePositive", "\"SizeBytes\" > 0");
            table.HasCheckConstraint("CK_EditorialMediaAssets_FocalPointPair", "(\"FocalPointX\" IS NULL AND \"FocalPointY\" IS NULL) OR (\"FocalPointX\" BETWEEN 0 AND 100 AND \"FocalPointY\" BETWEEN 0 AND 100)");
            table.HasCheckConstraint("CK_EditorialMediaAssets_ReadyTimestamp", "\"Status\" <> 2 OR \"ReadyAtUtc\" IS NOT NULL");
        });

        builder.HasKey(asset => asset.Id);
        builder.Property(asset => asset.ObjectPath).IsRequired().HasMaxLength(EditorialMediaAsset.ObjectPathMaxLength);
        builder.HasIndex(asset => asset.ObjectPath).IsUnique();
        builder.Property(asset => asset.OriginalFileName).IsRequired().HasMaxLength(EditorialMediaAsset.FileNameMaxLength);
        builder.Property(asset => asset.Kind).IsRequired().HasConversion<int>();
        builder.Property(asset => asset.Status).IsRequired().HasConversion<int>();
        builder.Property(asset => asset.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(asset => asset.SizeBytes).IsRequired();
        builder.Property(asset => asset.AltText).IsRequired().HasMaxLength(EditorialMediaAsset.AltTextMaxLength);
        builder.Property(asset => asset.Credit).IsRequired().HasMaxLength(EditorialMediaAsset.CreditMaxLength);
        builder.Property(asset => asset.Caption).HasMaxLength(EditorialMediaAsset.CaptionMaxLength);
        builder.Property(asset => asset.UploadedByUserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(asset => new { asset.Status, asset.CreatedAtUtc });
        builder.HasIndex(asset => new { asset.UploadedByUserId, asset.UploadExpiresAtUtc });
    }
}
