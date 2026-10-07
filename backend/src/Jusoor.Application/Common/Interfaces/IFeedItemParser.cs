using Jusoor.Application.Ingestion.Contracts;

namespace Jusoor.Application.Common.Interfaces;

/// <summary>
/// One implementation per supported feed format (RSS in this slice). The
/// ingestion command depends on this, not on any specific format, so adding
/// Atom or a source-specific JSON API later means adding a new
/// implementation, not touching the pipeline (§7: "additional source types
/// ... without rewriting the system").
/// </summary>
public interface IFeedItemParser
{
    /// <summary>Throws FeedParseException on malformed content — the caller
    /// classifies that as SourceFetchOutcome.ParseError and logs it; it must
    /// never crash the ingestion run for other sources.</summary>
    IReadOnlyList<FeedItemDto> Parse(string rawContent);
}

public class FeedParseException : Exception
{
    public FeedParseException(string message, Exception? inner = null) : base(message, inner) { }
}
