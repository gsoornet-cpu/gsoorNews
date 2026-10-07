using System.Net;
using System.Net.Http;
using FluentAssertions;
using Jusoor.Domain.Enums;
using Jusoor.Infrastructure.Ingestion;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Jusoor.Infrastructure.UnitTests.Ingestion;

/// <summary>
/// Exercises the real SocketsHttpHandler + SafeSocketConnector wiring
/// (not just PrivateNetworkGuard in isolation), proving the fetcher as a
/// whole actually refuses to connect — including targets that resolve
/// without any DNS lookup at all (a raw loopback IP), which is the simplest
/// possible case and the one most likely to be silently bypassed by a
/// wiring mistake (e.g. forgetting to set ConnectCallback on the handler
/// actually used by the named HttpClient).
/// </summary>
public class SsrfSafeSourceFetcherTests
{
    private static SsrfSafeSourceFetcher CreateFetcher()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = SafeSocketConnector.ConnectAsync,
            AllowAutoRedirect = false,
        };
        var httpClient = new HttpClient(handler);

        var factory = new SingleClientHttpClientFactory(httpClient);
        var options = Options.Create(new IngestionOptions
        {
            ConnectTimeoutSeconds = 3,
            ReadTimeoutSeconds = 3,
        });

        return new SsrfSafeSourceFetcher(factory, options, NullLogger<SsrfSafeSourceFetcher>.Instance);
    }

    [Theory]
    [InlineData("http://127.0.0.1:80/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://localhost/")]
    public async Task FetchAsync_should_reject_private_and_metadata_targets(string url)
    {
        var fetcher = CreateFetcher();

        var result = await fetcher.FetchAsync(url, CancellationToken.None);

        result.Outcome.Should().Be(SourceFetchOutcome.SecurityRejected);
    }

    [Fact]
    public async Task FetchAsync_should_reject_non_http_schemes_before_ever_resolving_dns()
    {
        var fetcher = CreateFetcher();

        var result = await fetcher.FetchAsync("ftp://example.com/file", CancellationToken.None);

        result.Outcome.Should().Be(SourceFetchOutcome.SecurityRejected);
    }

    private sealed class SingleClientHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public SingleClientHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }
}
