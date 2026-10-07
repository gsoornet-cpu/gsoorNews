using Jusoor.Domain.Common;

namespace Jusoor.Domain.Entities;

/// <summary>Editor-in-Chief managed primary newsroom taxonomy (Slice 23 / Q10).</summary>
public sealed class EditorialCategory : AuditableEntity
{
    public string NameAr { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    private EditorialCategory() { }

    public EditorialCategory(string nameAr, string slug, int displayOrder)
    {
        Rename(nameAr, slug, displayOrder);
    }

    public void Rename(string nameAr, string slug, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(nameAr) || nameAr.Trim().Length > 100)
            throw new ArgumentException("Category name must be 1–100 characters.", nameof(nameAr));
        if (string.IsNullOrWhiteSpace(slug) || slug.Trim().Length > 120 || slug.Any(c => !(char.IsLetterOrDigit(c) || c == '-')))
            throw new ArgumentException("Category slug must contain only letters, numbers and hyphens.", nameof(slug));
        if (displayOrder < 0) throw new ArgumentOutOfRangeException(nameof(displayOrder));
        NameAr = nameAr.Trim(); Slug = slug.Trim().ToLowerInvariant(); DisplayOrder = displayOrder;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
