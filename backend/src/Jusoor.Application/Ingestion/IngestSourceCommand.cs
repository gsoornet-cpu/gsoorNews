using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Ingestion.Contracts;
using Jusoor.Application.Relevance;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jusoor.Application.Ingestion;

public record IngestSourceCommand(Guid SourceId) : IRequest<IngestSourceResult>;

public record IngestSourceResult(
    bool Succeeded,
    SourceFetchOutcome Outcome,
    int ItemsFound,
    int ItemsIngested,
    int ItemsSkippedAsDuplicate,
    string? ErrorSummary);

public class IngestSourceCommandValidator : AbstractValidator<IngestSourceCommand>
{
    public IngestSourceCommandValidator()
    {
        RuleFor(x => x.SourceId).NotEmpty();
    }
}

public class IngestSourceCommandHandler : IRequestHandler<IngestSourceCommand, IngestSourceResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ISourceContentFetcher _fetcher;
    private readonly IFeedItemParser _feedParser;
    private readonly IStoryClusteringService _storyClusterer;
    private readonly IRelevanceEngine _relevanceEngine;
    private readonly RelevanceOptions _relevanceOptions;
    private readonly IGeographicExtractor _geographicExtractor;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<IngestSourceCommandHandler> _logger;

    public IngestSourceCommandHandler(
        IApplicationDbContext context,
        ISourceContentFetcher fetcher,
        IFeedItemParser feedParser,
        IStoryClusteringService storyClusterer,
        IRelevanceEngine relevanceEngine,
        IOptions<RelevanceOptions> relevanceOptions,
        IGeographicExtractor geographicExtractor,
        IDateTimeProvider clock,
        ILogger<IngestSourceCommandHandler> logger)
    {
        _context = context;
        _fetcher = fetcher;
        _feedParser = feedParser;
        _storyClusterer = storyClusterer;
        _relevanceEngine = relevanceEngine;
        _relevanceOptions = relevanceOptions.Value;
        _geographicExtractor = geographicExtractor;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IngestSourceResult> Handle(IngestSourceCommand request, CancellationToken cancellationToken)
    {
        var source = await _context.Sources.FirstOrDefaultAsync(s => s.Id == request.SourceId, cancellationToken);
        if (source is null)
        {
            _logger.LogWarning("IngestSourceCommand: source {SourceId} not found.", request.SourceId);
            return new IngestSourceResult(false, SourceFetchOutcome.HttpError, 0, 0, 0, "Source not found.");
        }

        if (!source.IsActive)
        {
            _logger.LogInformation("IngestSourceCommand: source {SourceId} is inactive, skipping.", request.SourceId);
            return new IngestSourceResult(true, SourceFetchOutcome.Success, 0, 0, 0, null);
        }

        if (string.IsNullOrWhiteSpace(source.FeedUrl))
        {
            const string reason = "Source has no FeedUrl configured.";
            await LogAndSaveAsync(source.Id, SourceFetchOutcome.HttpError, reason, cancellationToken: cancellationToken);
            return new IngestSourceResult(false, SourceFetchOutcome.HttpError, 0, 0, 0, reason);
        }

        var fetchResult = await _fetcher.FetchAsync(source.FeedUrl, cancellationToken);

        if (fetchResult.Outcome != SourceFetchOutcome.Success || fetchResult.RawContent is null)
        {
            _logger.LogWarning(
                "IngestSourceCommand: fetch failed for source {SourceId} with outcome {Outcome}: {Error}",
                source.Id, fetchResult.Outcome, fetchResult.ErrorSummary);

            await LogAndSaveAsync(
                source.Id, fetchResult.Outcome, fetchResult.ErrorSummary,
                httpStatusCode: fetchResult.HttpStatusCode, cancellationToken: cancellationToken);

            return new IngestSourceResult(false, fetchResult.Outcome, 0, 0, 0, fetchResult.ErrorSummary);
        }

        IReadOnlyList<FeedItemDto> items;
        try
        {
            items = _feedParser.Parse(fetchResult.RawContent);
        }
        catch (FeedParseException ex)
        {
            _logger.LogWarning(ex, "IngestSourceCommand: parse failed for source {SourceId}.", source.Id);
            await LogAndSaveAsync(source.Id, SourceFetchOutcome.ParseError, ex.Message, cancellationToken: cancellationToken);
            return new IngestSourceResult(false, SourceFetchOutcome.ParseError, 0, 0, 0, ex.Message);
        }

        // Pre-load known external IDs for this source to skip most duplicates
        // without a per-item query. This is NOT the only duplicate defense —
        // see the per-item save below for why it can't be, under concurrent
        // ingestion runs (TOCTOU between this read and the save).
        var knownExternalIds = await _context.Articles
            .Where(a => a.SourceId == source.Id)
            .Select(a => a.ExternalSourceItemId)
            .ToListAsync(cancellationToken);
        var knownExternalIdSet = new HashSet<string>(knownExternalIds, StringComparer.Ordinal);

        // Slice 6 — Geographic Extraction v1: the Country/City gazetteer
        // rarely changes within a single ingestion run, so it's loaded ONCE
        // here (with Cities eager-loaded) rather than once per Story, same
        // "preload, don't query per-item" reasoning as knownExternalIdSet
        // above. IGeographicExtractor takes it as a plain parameter — see
        // that interface's own comment for why.
        var knownCountries = await _context.Countries
            .Include(c => c.Cities)
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;
        var ingestedCount = 0;
        var duplicateCount = 0;

        foreach (var item in items)
        {
            if (knownExternalIdSet.Contains(item.ExternalId))
            {
                duplicateCount++;
                continue;
            }

            Article article;
            try
            {
                var contentHash = ContentHasher.ComputeHash(item.Title, item.RawContent);
                article = Article.Create(
                    source.Id, item.ExternalId, item.CanonicalUrl, item.Title,
                    contentHash, item.PublishedAtUtc, now, item.RawContent);
            }
            catch (ArgumentException ex)
            {
                // One malformed item (e.g. a non-http(s) link inside an
                // otherwise-valid feed) must not fail the whole batch —
                // §19: this is a validation error, logged and skipped, not
                // a reason to abort ingestion of the other items.
                _logger.LogWarning(ex, "IngestSourceCommand: skipping malformed item from source {SourceId}.", source.Id);
                continue;
            }

            _context.Articles.Add(article);

            // Cross-source exact-duplicate clustering (§12 / "Deduplication"
            // in the remaining Phase 1 scope): attach this Article to an
            // existing Story if another Source already published byte-
            // identical content, otherwise seed a brand-new Story for it.
            // Deliberately done BEFORE the save below (not as a separate
            // round-trip) so the Article insert, any new Story insert, and
            // the Article→Story FK all commit atomically in one
            // SaveChangesAsync — and so a duplicate-key failure on the
            // Article rolls the clustering decision back with it too.
            var clusteredStory = await _storyClusterer.ClusterAsync(article, now, cancellationToken);
            article.AdvanceTo(ArticleProcessingStatus.DuplicateCheck);

            // Slice 6 — Geographic Extraction v1: matches spec §06's
            // pipeline order (location extraction runs before relevance
            // classification). Independent of the relevance verdict below —
            // Story.SetGeography and Story.ApplyRelevanceVerdict touch
            // disjoint fields, so running this first changes nothing about
            // Slice 5's already-verified relevance behavior, only adds a
            // geography assignment alongside it. Re-run on every new
            // Article the same way relevance is, for the same reason: a
            // Story that gains a second source's coverage may name a city
            // the first source didn't.
            var geoResult = await _geographicExtractor.ExtractAsync(clusteredStory, knownCountries, cancellationToken);
            clusteredStory.SetGeography(geoResult.CountryId, geoResult.CityId);

            // Relevance Engine v1 (Slice 5): re-evaluate the Story every
            // time a new Article joins it — a Story that gains a second
            // source's coverage deserves a fresh look, and RelevanceResult
            // is deliberately append-only (see its own doc comment) so this
            // never destroys the previous verdict's audit trail. Folded into
            // the SAME save as the Article/Story write below — no extra
            // round-trip, and a duplicate-key failure rolls this verdict
            // back too, same reasoning as the clustering step above.
            var evaluation = await _relevanceEngine.EvaluateAsync(clusteredStory, cancellationToken);
            var relevanceResult = RelevanceResult.Create(
                clusteredStory.Id, article.Id, evaluation.IsRelevant, evaluation.ConfidenceScore,
                evaluation.Method, evaluation.Reasons, evaluation.EngineVersion, now);
            _context.RelevanceResults.Add(relevanceResult);
            clusteredStory.ApplyRelevanceVerdict(evaluation.IsRelevant, evaluation.ConfidenceScore, _relevanceOptions.ReviewThreshold, now);
            article.AdvanceTo(ArticleProcessingStatus.RelevanceEvaluated);

            try
            {
                // Saved per-item, not batched, specifically so that a
                // duplicate-key race (two concurrent ingestion runs picking
                // up the same new item) only loses the ONE conflicting
                // article, not every genuinely-new article fetched in this
                // same run. See §18 — retries must be safe.
                await _context.SaveChangesAsync(cancellationToken);
                ingestedCount++;
                knownExternalIdSet.Add(item.ExternalId);
            }
            catch (DbUpdateException)
            {
                duplicateCount++;
                // Detach every entity this item touched so a later item's
                // SaveChangesAsync in this same loop doesn't try to re-insert
                // any of them again. Detaching an already-existing Story we
                // merely looked up (rather than created) is also safe — it's
                // unmodified and simply gets re-fetched fresh if needed
                // again later in this same run.
                _context.Detach(article);
                _context.Detach(clusteredStory);
                _context.Detach(relevanceResult);
            }
        }

        await LogAndSaveAsync(
            source.Id, SourceFetchOutcome.Success, errorSummary: null,
            itemsFound: items.Count, itemsIngested: ingestedCount, itemsSkippedAsDuplicate: duplicateCount,
            httpStatusCode: fetchResult.HttpStatusCode, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "IngestSourceCommand: source {SourceId} — found {Found}, ingested {Ingested}, duplicates {Duplicates}.",
            source.Id, items.Count, ingestedCount, duplicateCount);

        return new IngestSourceResult(true, SourceFetchOutcome.Success, items.Count, ingestedCount, duplicateCount, null);
    }

    /// <summary>Detaching (rather than just leaving it Added) matters here —
    /// without it, the next SaveChangesAsync call for a later item in the
    /// same loop would try to re-insert this same failed entity again.
    /// This is handled via IApplicationDbContext.Detach — see that
    /// interface's comment for why we don't cast to the concrete DbContext
    /// here instead.</summary>
    private async Task LogAndSaveAsync(
        Guid sourceId,
        SourceFetchOutcome outcome,
        string? errorSummary,
        int itemsFound = 0,
        int itemsIngested = 0,
        int itemsSkippedAsDuplicate = 0,
        int? httpStatusCode = null,
        CancellationToken cancellationToken = default)
    {
        var log = SourceFetchLog.Start(sourceId, _clock.UtcNow);
        log.Complete(outcome, _clock.UtcNow, itemsFound, itemsIngested, itemsSkippedAsDuplicate, httpStatusCode, errorSummary);
        _context.SourceFetchLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
    }
}