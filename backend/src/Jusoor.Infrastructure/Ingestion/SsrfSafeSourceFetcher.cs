using System.Text;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Ingestion.Contracts;
using Jusoor.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jusoor.Infrastructure.Ingestion;

public class SsrfSafeSourceFetcher : ISourceContentFetcher
{
    private readonly HttpClient _httpClient;
    private readonly IngestionOptions _options;
    private readonly ILogger<SsrfSafeSourceFetcher> _logger;

    public SsrfSafeSourceFetcher(
        IHttpClientFactory httpClientFactory,
        IOptions<IngestionOptions> options,
        ILogger<SsrfSafeSourceFetcher> logger)
    {
        // Named client — see DependencyInjection.cs for where the
        // SocketsHttpHandler + SafeSocketConnector + AllowAutoRedirect=false
        // are actually configured. Keeping that wiring in DI (rather than
        // constructing a handler here) is what lets IHttpClientFactory
        // manage the handler's lifetime correctly.
        _httpClient = httpClientFactory.CreateClient(IngestionHttpClientNames.SourceFetcher);
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SourceFetchResult> FetchAsync(string url, CancellationToken cancellationToken)
    {
        var currentUrl = url;

        for (var redirectCount = 0; redirectCount <= _options.MaxRedirects; redirectCount++)
        {
            if (!TryValidateUrlShape(currentUrl, out var uri, out var shapeError))
            {
                return SourceFetchResult.Failure(SourceFetchOutcome.SecurityRejected, shapeError!);
            }

            using var timeoutCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(_options.ConnectTimeoutSeconds + _options.ReadTimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            HttpResponseMessage response;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return SourceFetchResult.Failure(SourceFetchOutcome.Timeout, $"Timed out fetching '{uri}'.");
            }
            catch (HttpRequestException ex)
            {
                // SafeSocketConnector throws SsrfViolationException from
                // inside ConnectCallback; the runtime wraps it in
                // HttpRequestException. Unwrap it so the caller gets the
                // correct classification (SecurityRejected, never retried
                // as-is) instead of a generic transient-network failure.
                if (UnwrapSsrfViolation(ex) is { } ssrfMessage)
                {
                    _logger.LogWarning("SSRF guard rejected '{Uri}': {Reason}", uri, ssrfMessage);
                    return SourceFetchResult.Failure(SourceFetchOutcome.SecurityRejected, ssrfMessage);
                }

                return SourceFetchResult.Failure(SourceFetchOutcome.TransientNetworkError, ex.Message);
            }

            using (response)
            {
                if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is not null)
                {
                    if (redirectCount == _options.MaxRedirects)
                    {
                        return SourceFetchResult.Failure(
                            SourceFetchOutcome.SecurityRejected, $"Exceeded max redirects ({_options.MaxRedirects}) for '{url}'.");
                    }

                    var location = response.Headers.Location;
                    currentUrl = location.IsAbsoluteUri ? location.ToString() : new Uri(uri, location).ToString();
                    // Loop again: the NEW url goes through TryValidateUrlShape
                    // and a fresh ConnectCallback resolution/validation —
                    // redirects are not exempt from the SSRF guard.
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return SourceFetchResult.Failure(
                        SourceFetchOutcome.HttpError, $"HTTP {(int)response.StatusCode} from '{uri}'.", (int)response.StatusCode);
                }

                var content = await ReadBoundedAsync(response, _options.MaxResponseBytes, linkedCts.Token);
                if (content is null)
                {
                    return SourceFetchResult.Failure(
                        SourceFetchOutcome.ResponseTooLarge,
                        $"Response from '{uri}' exceeded the {_options.MaxResponseBytes}-byte cap.",
                        (int)response.StatusCode);
                }

                return SourceFetchResult.Success(content, (int)response.StatusCode);
            }
        }

        return SourceFetchResult.Failure(SourceFetchOutcome.SecurityRejected, $"Exceeded max redirects ({_options.MaxRedirects}) for '{url}'.");
    }

    private static bool TryValidateUrlShape(string url, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Uri? uri, out string? error)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            uri = null;
            error = $"'{url}' is not an absolute http(s) URL.";
            return false;
        }

        error = null;
        return true;
    }

    private static string? UnwrapSsrfViolation(Exception ex)
    {
        var current = ex;
        while (current is not null)
        {
            if (current is SsrfViolationException ssrf)
            {
                return ssrf.Message;
            }

            current = current.InnerException;
        }

        return null;
    }

    /// <summary>Reads at most maxBytes; returns null if the response is
    /// larger (rather than silently truncating, which would hand the
    /// parser corrupt/incomplete XML and misreport it as a parse error
    /// instead of the actual cause).</summary>
    private static async Task<string?> ReadBoundedAsync(HttpResponseMessage response, int maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];

        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + bytesRead > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, bytesRead);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}

public static class IngestionHttpClientNames
{
    public const string SourceFetcher = "SourceFetcher";
}
