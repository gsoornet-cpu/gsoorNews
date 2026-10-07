using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Jusoor.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Jusoor.Infrastructure.Content;

public sealed class SupabaseEditorialMediaStorageOptions
{
    public string? Url { get; set; }
    public string? Bucket { get; set; }
    public string? ServiceRoleKey { get; set; }
}

/// <summary>
/// Server-only Supabase Storage REST adapter. The service-role credential is
/// applied to server-side signing/inspection/cleanup calls and is never returned
/// to the browser. Actual file bytes travel directly from the CMS browser to the
/// one-object signed upload URL.
/// </summary>
public sealed class SupabaseEditorialMediaStorage : IEditorialMediaStorage
{
    private readonly HttpClient _client;
    private readonly SupabaseEditorialMediaStorageOptions _options;

    public SupabaseEditorialMediaStorage(HttpClient client, IOptions<SupabaseEditorialMediaStorageOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task<SignedMediaUpload> CreateSignedUploadAsync(string objectPath, CancellationToken cancellationToken)
    {
        var (baseUrl, bucket, key) = GetConfiguration();
        using var request = new HttpRequestMessage(HttpMethod.Post, StorageUri(baseUrl, $"object/upload/sign/{EscapePath(bucket)}/{EscapePath(objectPath)}"));
        AddServiceKey(request, key);
        request.Headers.TryAddWithoutValidation("x-upsert", "false");
        request.Content = JsonContent.Create(new { });

        using var response = await SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new EditorialMediaStorageException("signing_failed");
        }

        try
        {
            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var path = body.RootElement.GetProperty("url").GetString();
            if (string.IsNullOrWhiteSpace(path)) throw new JsonException();
            var signedUri = path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? new Uri(path, UriKind.Absolute)
                : StorageUri(baseUrl, path.TrimStart('/'));
            var token = signedUri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split('=', 2))
                .Where(parts => parts.Length == 2 && parts[0] == "token")
                .Select(parts => Uri.UnescapeDataString(parts[1]))
                .FirstOrDefault();

            if (signedUri.Scheme != Uri.UriSchemeHttps || signedUri.Host != new Uri(baseUrl).Host
                || !IsCompactJws(token))
            {
                throw new JsonException();
            }

            // Supabase currently issues signed-upload tokens with a two-hour lifetime.
            // The database reservation uses the same conservative expiry window.
            return new SignedMediaUpload(signedUri.ToString(), token!, bucket, objectPath,
                ResumableStorageUri(baseUrl).ToString(), DateTimeOffset.UtcNow.AddHours(2));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or UriFormatException)
        {
            throw new EditorialMediaStorageException("invalid_signing_response");
        }
    }

    public async Task<StoredMediaObject?> InspectPublicObjectAsync(string objectPath, CancellationToken cancellationToken)
    {
        var (baseUrl, bucket, _) = GetConfiguration();
        var publicUri = StorageUri(baseUrl, $"object/public/{EscapePath(bucket)}/{EscapePath(objectPath)}");

        using var headRequest = new HttpRequestMessage(HttpMethod.Head, publicUri);
        using var head = await SendAsync(headRequest, cancellationToken);
        if (head.StatusCode == HttpStatusCode.NotFound) return null;
        if (!head.IsSuccessStatusCode)
        {
            throw new EditorialMediaStorageException("object_inspection_failed");
        }

        var length = head.Content.Headers.ContentLength;
        var contentType = head.Content.Headers.ContentType?.MediaType;
        if (length is null || string.IsNullOrWhiteSpace(contentType))
        {
            throw new EditorialMediaStorageException("object_metadata_missing");
        }

        using var prefixRequest = new HttpRequestMessage(HttpMethod.Get, publicUri);
        prefixRequest.Headers.Range = new RangeHeaderValue(0, 63);
        using var prefixResponse = await SendAsync(prefixRequest, cancellationToken);
        if (prefixResponse.StatusCode is not (HttpStatusCode.PartialContent or HttpStatusCode.OK))
        {
            throw new EditorialMediaStorageException("object_signature_unavailable");
        }

        await using var stream = await prefixResponse.Content.ReadAsStreamAsync(cancellationToken);
        var prefix = new byte[64];
        var read = 0;
        while (read < prefix.Length)
        {
            var next = await stream.ReadAsync(prefix.AsMemory(read), cancellationToken);
            if (next == 0) break;
            read += next;
        }
        Array.Resize(ref prefix, read);
        return new StoredMediaObject(length.Value, contentType, prefix);
    }

    public async Task DeleteObjectAsync(string objectPath, CancellationToken cancellationToken)
    {
        var (baseUrl, bucket, key) = GetConfiguration();
        using var request = new HttpRequestMessage(HttpMethod.Delete, StorageUri(baseUrl, $"object/{EscapePath(bucket)}/{EscapePath(objectPath)}"));
        AddServiceKey(request, key);
        using var response = await SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
        {
            throw new EditorialMediaStorageException("object_cleanup_failed");
        }
    }

    public string GetPublicUrl(string objectPath)
    {
        var (baseUrl, bucket, _) = GetConfiguration();
        return StorageUri(baseUrl, $"object/public/{EscapePath(bucket)}/{EscapePath(objectPath)}").ToString();
    }

    private (string Url, string Bucket, string Key) GetConfiguration()
    {
        var url = _options.Url?.Trim().TrimEnd('/');
        var bucket = _options.Bucket?.Trim();
        var key = _options.ServiceRoleKey?.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
            || string.IsNullOrWhiteSpace(bucket) || bucket.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-'))
            || string.IsNullOrWhiteSpace(key))
        {
            throw new EditorialMediaStorageException("storage_configuration_missing");
        }

        return (url!, bucket!, key!);
    }

    private static Uri StorageUri(string baseUrl, string relativePath)
        => new($"{baseUrl}/storage/v1/{relativePath.TrimStart('/')}", UriKind.Absolute);

    private static Uri ResumableStorageUri(string baseUrl)
    {
        var projectUri = new Uri(baseUrl, UriKind.Absolute);
        var hostParts = projectUri.Host.Split('.');
        // Supabase recommends the direct Storage hostname for TUS uploads.
        // Keep custom domains and non-standard project hosts unchanged.
        if (hostParts.Length == 3 && hostParts[1] == "supabase" && hostParts[2] == "co")
        {
            projectUri = new UriBuilder(projectUri) { Host = $"{hostParts[0]}.storage.supabase.co" }.Uri;
        }

        return new UriBuilder(projectUri) { Path = "/storage/v1/upload/resumable", Query = string.Empty, Fragment = string.Empty }.Uri;
    }

    private static bool IsCompactJws(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        var parts = token.Split('.');
        if (parts.Length != 3 || parts.Any(part => part.Length == 0 || part.Length % 4 == 1
            || part.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))))
        {
            return false;
        }

        try
        {
            var header = DecodeBase64Url(parts[0]);
            var payload = DecodeBase64Url(parts[1]);
            _ = DecodeBase64Url(parts[2]);
            using var parsedHeader = JsonDocument.Parse(header);
            using var parsedPayload = JsonDocument.Parse(payload);
            return parsedHeader.RootElement.ValueKind == JsonValueKind.Object
                && parsedHeader.RootElement.TryGetProperty("alg", out var algorithm)
                && algorithm.ValueKind == JsonValueKind.String
                && parsedPayload.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return false;
        }
    }

    private static byte[] DecodeBase64Url(string segment)
    {
        var base64 = segment.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
        return Convert.FromBase64String(base64);
    }

    private static string EscapePath(string path)
        => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    private static void AddServiceKey(HttpRequestMessage request, string key)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Headers.TryAddWithoutValidation("apikey", key);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new EditorialMediaStorageException("storage_request_failed");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EditorialMediaStorageException("storage_request_timed_out");
        }
    }
}
