using Jusoor.Application.Ingestion.Contracts;

namespace Jusoor.Application.Common.Interfaces;

/// <summary>
/// Fetches raw content from a source URL. The concrete implementation
/// (Infrastructure) is where SSRF protection, timeouts, redirect handling,
/// and response-size limits live — none of that is an Application-layer
/// concern. Application only knows "give me content or a classified
/// failure", which is what keeps this layer testable with a fake instead of
/// a real network stack.
/// </summary>
public interface ISourceContentFetcher
{
    Task<SourceFetchResult> FetchAsync(string url, CancellationToken cancellationToken);
}
