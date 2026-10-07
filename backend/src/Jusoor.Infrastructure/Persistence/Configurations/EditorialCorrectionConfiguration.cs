using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jusoor.Infrastructure.Persistence.Configurations;

public class EditorialCorrectionConfiguration : IEntityTypeConfiguration<EditorialCorrection>
{
    public void Configure(EntityTypeBuilder<EditorialCorrection> builder)
    {
        builder.ToTable("EditorialCorrections");
        builder.HasKey(c => c.Id);

        // No foreign key: corrections are an append-only public record and must
        // survive whatever happens to the article row.
        builder.Property(c => c.ArticleId).IsRequired();
        builder.Property(c => c.Kind).IsRequired().HasConversion<int>();
        builder.Property(c => c.Note).IsRequired().HasMaxLength(EditorialCorrection.NoteMaxLength);
        builder.Property(c => c.IsMajor).IsRequired();
        builder.Property(c => c.IssuedByUserId).IsRequired().HasMaxLength(450);
        builder.Property(c => c.IssuerRoles).IsRequired().HasMaxLength(EditorialCorrection.IssuerRolesMaxLength);

        builder.HasIndex(c => new { c.ArticleId, c.IssuedAtUtc });
    }
}
