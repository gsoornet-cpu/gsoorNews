using Jusoor.Domain.Common;
using Jusoor.Domain.Enums;

namespace Jusoor.Domain.Entities;

/// <summary>A CMS assignment of a ready image asset to a home-page slot and surface.</summary>
public sealed class EditorialAdPlacement : AuditableEntity
{
    public const int SlotKeyMaxLength = 80;
    public const int TargetUrlMaxLength = 2048;
    public const int AltTextMaxLength = 500;

    public string SlotKey { get; private set; } = null!;
    public EditorialAdSurface Surface { get; private set; }
    public Guid MediaAssetId { get; private set; }
    public EditorialMediaAsset MediaAsset { get; private set; } = null!;
    public string TargetUrl { get; private set; } = null!;
    public string AltText { get; private set; } = null!;
    public int DisplayOrder { get; private set; }

    private EditorialAdPlacement() { }

    public static EditorialAdPlacement Create(string slotKey, EditorialAdSurface surface, Guid mediaAssetId,
        string targetUrl, string altText, int displayOrder)
    {
        var placement = new EditorialAdPlacement();
        placement.Update(slotKey, surface, mediaAssetId, targetUrl, altText, displayOrder);
        return placement;
    }

    public void Update(string slotKey, EditorialAdSurface surface, Guid mediaAssetId,
        string targetUrl, string altText, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(slotKey) || slotKey.Length > SlotKeyMaxLength)
            throw new ArgumentException("Invalid advertisement slot.", nameof(slotKey));
        if (mediaAssetId == Guid.Empty) throw new ArgumentException("An image asset is required.", nameof(mediaAssetId));
        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")
            || targetUrl.Length > TargetUrlMaxLength)
            throw new ArgumentException("Target URL must use HTTP or HTTPS.", nameof(targetUrl));
        if (string.IsNullOrWhiteSpace(altText) || altText.Trim().Length > AltTextMaxLength)
            throw new ArgumentException("Accessible alternative text is required.", nameof(altText));
        if (displayOrder < 0) throw new ArgumentOutOfRangeException(nameof(displayOrder));

        SlotKey = slotKey.Trim();
        Surface = surface;
        MediaAssetId = mediaAssetId;
        TargetUrl = uri.ToString();
        AltText = altText.Trim();
        DisplayOrder = displayOrder;
    }
}
