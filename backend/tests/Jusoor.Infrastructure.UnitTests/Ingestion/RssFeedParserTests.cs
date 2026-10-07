using FluentAssertions;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Infrastructure.Ingestion;
using Xunit;

namespace Jusoor.Infrastructure.UnitTests.Ingestion;

public class RssFeedParserTests
{
    private readonly RssFeedParser _parser = new();

    private const string ValidRss = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0">
          <channel>
            <title>Test Feed</title>
            <item>
              <title>خبر تجريبي</title>
              <link>https://example.com/news/1</link>
              <guid>urn:example:1</guid>
              <pubDate>Tue, 01 Sep 2026 10:00:00 GMT</pubDate>
              <description>Summary text</description>
            </item>
            <item>
              <title>Second item</title>
              <link>https://example.com/news/2</link>
            </item>
          </channel>
        </rss>
        """;

    [Fact]
    public void Parse_should_extract_items_from_valid_rss()
    {
        var items = _parser.Parse(ValidRss);

        items.Should().HaveCount(2);
        items[0].Title.Should().Be("خبر تجريبي");
        items[0].CanonicalUrl.Should().Be("https://example.com/news/1");
        items[0].ExternalId.Should().Be("urn:example:1");
        items[0].PublishedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Parse_should_fall_back_to_link_as_external_id_when_no_guid_present()
    {
        var items = _parser.Parse(ValidRss);
        items[1].ExternalId.Should().Be("https://example.com/news/2");
    }

    [Fact]
    public void Parse_should_throw_FeedParseException_for_malformed_xml()
    {
        var act = () => _parser.Parse("<rss><this is not><valid xml");
        act.Should().Throw<FeedParseException>();
    }

    [Fact]
    public void Parse_should_reject_xxe_attempts_rather_than_resolve_external_entities()
    {
        // A classic XXE payload: if DTD processing / external entity
        // resolution were NOT disabled, this would attempt to read a local
        // file (or worse, an internal network resource via a crafted
        // SYSTEM URI) and inline its content into the parsed title. With
        // DtdProcessing.Prohibit, this must fail fast with a
        // FeedParseException instead of ever attempting resolution.
        const string xxePayload = """
            <?xml version="1.0"?>
            <!DOCTYPE rss [<!ENTITY xxe SYSTEM "file:///etc/passwd">]>
            <rss version="2.0">
              <channel>
                <item>
                  <title>&xxe;</title>
                  <link>https://example.com/x</link>
                </item>
              </channel>
            </rss>
            """;

        var act = () => _parser.Parse(xxePayload);

        act.Should().Throw<FeedParseException>();
    }
}
