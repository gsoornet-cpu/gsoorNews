using System.ServiceModel.Syndication;
using System.Xml;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Ingestion.Contracts;

namespace Jusoor.Infrastructure.Ingestion;

/// <summary>
/// Feed content is untrusted external input (§15's "treat article/source
/// content as hostile input" applies to the XML itself, not just to text
/// later reaching an LLM) — the XmlReaderSettings below specifically
/// disable DTD processing and external entity resolution to close the
/// classic XXE (XML External Entity) attack a naive XmlReader would be
/// vulnerable to.
/// </summary>
public class RssFeedParser : IFeedItemParser
{
    public IReadOnlyList<FeedItemDto> Parse(string rawContent)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 1024,
        };

        try
        {
            using var stringReader = new StringReader(rawContent);
            using var xmlReader = XmlReader.Create(stringReader, settings);
            var feed = SyndicationFeed.Load(xmlReader)
                ?? throw new FeedParseException("SyndicationFeed.Load returned null.");

            return feed.Items.Select(ToFeedItemDto).Where(item => item is not null).Select(item => item!).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // SyndicationFeed.Load can throw more than XmlException for
            // malformed untrusted input — an empty/unsupported RSS version
            // attribute throws NotSupportedException, for example. Since
            // feed content is untrusted external data (see class summary),
            // every failure mode here is classified the same way: a parse
            // failure to log and skip, never an unhandled exception type
            // escaping this boundary.
            throw new FeedParseException($"Feed content could not be parsed as RSS/Atom: {ex.Message}", ex);
        }
    }

    private static FeedItemDto? ToFeedItemDto(SyndicationItem item)
    {
        var link = item.Links.FirstOrDefault(l => l.RelationshipType is null or "alternate")?.Uri
            ?? item.Links.FirstOrDefault()?.Uri;

        if (link is null)
        {
            // An item with no link at all can't become an Article (Article.Create
            // requires a valid canonical URL) — skip it rather than fail the
            // whole feed, same "one bad item doesn't sink the batch" principle
            // as IngestSourceCommandHandler applies to malformed items.
            return null;
        }

        var externalId = !string.IsNullOrWhiteSpace(item.Id) ? item.Id : link.ToString();
        var title = item.Title?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var rawContent = item.Summary?.Text
            ?? (item.Content as TextSyndicationContent)?.Text;

        // SyndicationItem.PublishDate THROWS (rather than returning a
        // default) when the underlying pubDate string doesn't parse — real
        // RSS feeds in the wild frequently have nonstandard date formats.
        // One bad date must not fail this item's ingestion (let alone the
        // whole feed's), so it's treated as "no publish date" rather than
        // propagating.
        DateTimeOffset? publishedAtUtc;
        try
        {
            publishedAtUtc = item.PublishDate != default ? item.PublishDate.ToUniversalTime() : null;
        }
        catch (Exception)
        {
            publishedAtUtc = null;
        }

        return new FeedItemDto(externalId, title, link.ToString(), rawContent, publishedAtUtc);
    }
}
