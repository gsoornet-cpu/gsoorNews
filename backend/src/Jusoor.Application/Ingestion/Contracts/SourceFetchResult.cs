using Jusoor.Domain.Enums;

namespace Jusoor.Application.Ingestion.Contracts;

public record SourceFetchResult(
    SourceFetchOutcome Outcome,
    string? RawContent,
    int? HttpStatusCode,
    string? ErrorSummary)
{
    public static SourceFetchResult Success(string rawContent, int httpStatusCode) =>
        new(SourceFetchOutcome.Success, rawContent, httpStatusCode, null);

    public static SourceFetchResult Failure(SourceFetchOutcome outcome, string errorSummary, int? httpStatusCode = null) =>
        new(outcome, null, httpStatusCode, errorSummary);
}
