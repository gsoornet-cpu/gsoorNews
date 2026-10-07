using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jusoor.Infrastructure.Persistence.Configurations;

public sealed class EditorialCategoryConfiguration : IEntityTypeConfiguration<EditorialCategory>
{
    public void Configure(EntityTypeBuilder<EditorialCategory> builder)
    {
        builder.ToTable("EditorialCategories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.NameAr).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Slug).IsRequired().HasMaxLength(120);
        builder.HasIndex(c => c.Slug).IsUnique();
        builder.HasIndex(c => new { c.IsActive, c.DisplayOrder });
    }
}
