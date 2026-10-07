using Jusoor.Domain.Enums;

namespace Jusoor.Application.Editorial;

/// <summary>Server-side file policy derived from D3; client-provided MIME and suffix are only hints.</summary>
public static class EditorialMediaPolicy
{
    public const long ImageMaxBytes = 10 * 1024 * 1024;
    public const long VideoMaxBytes = 100 * 1024 * 1024;
    public const long PdfMaxBytes = 20 * 1024 * 1024;

    public static bool TryGetKind(string fileName, string contentType, long sizeBytes, out EditorialMediaKind kind)
    {
        kind = default;
        if (sizeBytes <= 0 || string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(contentType)) return false;

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var mediaType = contentType.Trim().ToLowerInvariant();
        (kind, var maxBytes, var extensions, var mimeTypes) = mediaType switch
        {
            "image/jpeg" => (EditorialMediaKind.Image, ImageMaxBytes, new[] { ".jpg", ".jpeg" }, new[] { "image/jpeg" }),
            "image/png" => (EditorialMediaKind.Image, ImageMaxBytes, new[] { ".png" }, new[] { "image/png" }),
            "image/webp" => (EditorialMediaKind.Image, ImageMaxBytes, new[] { ".webp" }, new[] { "image/webp" }),
            "image/avif" => (EditorialMediaKind.Image, ImageMaxBytes, new[] { ".avif" }, new[] { "image/avif" }),
            "video/mp4" => (EditorialMediaKind.Video, VideoMaxBytes, new[] { ".mp4" }, new[] { "video/mp4" }),
            "video/webm" => (EditorialMediaKind.Video, VideoMaxBytes, new[] { ".webm" }, new[] { "video/webm" }),
            "application/pdf" => (EditorialMediaKind.Document, PdfMaxBytes, new[] { ".pdf" }, new[] { "application/pdf" }),
            _ => (default, 0L, Array.Empty<string>(), Array.Empty<string>())
        };

        return maxBytes > 0 && sizeBytes <= maxBytes && extensions.Contains(extension, StringComparer.Ordinal)
            && mimeTypes.Contains(mediaType, StringComparer.Ordinal);
    }

    public static bool HasExpectedSignature(EditorialMediaKind kind, string contentType, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12) return false;

        return (kind, contentType.ToLowerInvariant()) switch
        {
            (EditorialMediaKind.Image, "image/jpeg") => bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            (EditorialMediaKind.Image, "image/png") => bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            (EditorialMediaKind.Image, "image/webp") => bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8),
            (EditorialMediaKind.Image, "image/avif") => bytes[4..8].SequenceEqual("ftyp"u8)
                && (bytes[8..12].SequenceEqual("avif"u8) || bytes[8..12].SequenceEqual("avis"u8)),
            (EditorialMediaKind.Video, "video/mp4") => bytes[4..8].SequenceEqual("ftyp"u8),
            (EditorialMediaKind.Video, "video/webm") => bytes[..4].SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }),
            (EditorialMediaKind.Document, "application/pdf") => bytes[..5].SequenceEqual("%PDF-"u8),
            _ => false
        };
    }
}
