using System.Security.Cryptography;
using System.Text;

namespace Jusoor.Application.Ingestion;

/// <summary>
/// SHA-256 over a normalized (trimmed) title+content pair. Pure BCL
/// cryptography, not an infrastructure dependency, so this is fine to live
/// in Application — it's a deterministic function, not I/O.
/// </summary>
public static class ContentHasher
{
    public static string ComputeHash(string title, string? rawContent)
    {
        var normalized = $"{title.Trim()}|{(rawContent ?? string.Empty).Trim()}";
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
