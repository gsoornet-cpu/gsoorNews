using Jusoor.Domain.Common;
using Jusoor.Domain.Enums;

namespace Jusoor.Domain.Entities;

/// <summary>
/// A newsroom-owned asset in Supabase Storage. A row is created before its
/// short-lived upload URL is issued and becomes selectable only after the API
/// verifies the uploaded object. The object path is server-generated and never
/// comes from a client filename.
/// </summary>
public sealed class EditorialMediaAsset : AuditableEntity
{
    public const int ObjectPathMaxLength = 512;
    public const int FileNameMaxLength = 255;
    public const int AltTextMaxLength = 500;
    public const int CreditMaxLength = 300;
    public const int CaptionMaxLength = 500;

    public string ObjectPath { get; private set; } = null!;
    public string OriginalFileName { get; private set; } = null!;
    public EditorialMediaKind Kind { get; private set; }
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public string AltText { get; private set; } = null!;
    public string Credit { get; private set; } = null!;
    public string? Caption { get; private set; }
    public int? FocalPointX { get; private set; }
    public int? FocalPointY { get; private set; }
    public string UploadedByUserId { get; private set; } = null!;
    public EditorialMediaStatus Status { get; private set; }
    public DateTimeOffset UploadExpiresAtUtc { get; private set; }
    public DateTimeOffset? ReadyAtUtc { get; private set; }

    private EditorialMediaAsset() { }

    public static EditorialMediaAsset CreatePending(
        string objectPath,
        string originalFileName,
        EditorialMediaKind kind,
        string contentType,
        long sizeBytes,
        string altText,
        string credit,
        string? caption,
        int? focalPointX,
        int? focalPointY,
        string uploadedByUserId,
        DateTimeOffset expiresAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(altText);
        ArgumentException.ThrowIfNullOrWhiteSpace(credit);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadedByUserId);
        if (objectPath.Length > ObjectPathMaxLength || originalFileName.Length > FileNameMaxLength
            || altText.Trim().Length > AltTextMaxLength || credit.Trim().Length > CreditMaxLength
            || (caption?.Trim().Length ?? 0) > CaptionMaxLength)
        {
            throw new ArgumentException("Media metadata exceeds its allowed length.");
        }

        if (sizeBytes <= 0 || (focalPointX.HasValue != focalPointY.HasValue)
            || focalPointX is < 0 or > 100 || focalPointY is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "Media size and optional focal coordinates are invalid.");
        }

        return new EditorialMediaAsset
        {
            ObjectPath = objectPath,
            OriginalFileName = originalFileName.Trim(),
            Kind = kind,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            AltText = altText.Trim(),
            Credit = credit.Trim(),
            Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim(),
            FocalPointX = focalPointX,
            FocalPointY = focalPointY,
            UploadedByUserId = uploadedByUserId,
            Status = EditorialMediaStatus.PendingUpload,
            UploadExpiresAtUtc = expiresAtUtc
        };
    }

    public void MarkReady(long verifiedSizeBytes, string verifiedContentType, DateTimeOffset nowUtc)
    {
        if (Status != EditorialMediaStatus.PendingUpload)
        {
            throw new InvalidOperationException("Only a pending media upload can be finalized.");
        }

        if (nowUtc > UploadExpiresAtUtc || verifiedSizeBytes != SizeBytes
            || !string.Equals(verifiedContentType, ContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The uploaded object does not match its signed upload reservation.");
        }

        Status = EditorialMediaStatus.Ready;
        ReadyAtUtc = nowUtc;
    }

    public void MarkUploadFailed()
    {
        if (Status == EditorialMediaStatus.PendingUpload)
        {
            Status = EditorialMediaStatus.Failed;
        }
    }
}
