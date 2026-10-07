using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Jusoor.Domain.Editorial;

namespace Jusoor.Application.Editorial;

public sealed record UpdateEditorialArticleCommand(
    Guid ArticleId,
    string Title,
    string? Summary,
    string Body,
    Guid? CountryId,
    Guid? CityId,
    string ActorUserId,
    IReadOnlyList<string> ActorRoles,
    string? EditReason = null,
    string[]? PresentationDesks = null,
    Guid? FeaturedVideoMediaAssetId = null,
    string? CoverImageUrl = null,
    string? AuthorName = null) : IRequest<EditorialArticleResult>;

public class UpdateEditorialArticleCommandValidator : AbstractValidator<UpdateEditorialArticleCommand>
{
    public UpdateEditorialArticleCommandValidator()
    {
        RuleFor(x => x.ArticleId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(EditorialArticle.TitleMaxLength);
        RuleFor(x => x.Summary).MaximumLength(EditorialArticle.SummaryMaxLength);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(EditorialArticle.BodyMaxLength);
        RuleFor(x => x.CoverImageUrl).MaximumLength(2048);
        RuleFor(x => x.AuthorName).MaximumLength(120);
        RuleFor(x => x.CountryId).NotNull().When(x => x.CityId.HasValue)
            .WithMessage("A city cannot be set without its country.");
        RuleFor(x => x.EditReason).MaximumLength(EditorialArticleRevision.ReasonMaxLength);
        RuleFor(x => x.PresentationDesks).Must(desks => desks is null ||
            (desks.Length <= 13 && desks.All(EditorialDesk.IsValid) && desks.Distinct(StringComparer.Ordinal).Count() == desks.Length));
    }
}

public class UpdateEditorialArticleCommandHandler : IRequestHandler<UpdateEditorialArticleCommand, EditorialArticleResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;
    private readonly IEditorialHtmlSanitizer _sanitizer;
    private readonly ILogger<UpdateEditorialArticleCommandHandler> _logger;
    private readonly IEditorialMediaReferenceValidator? _mediaReferences;

    public UpdateEditorialArticleCommandHandler(
        IApplicationDbContext context,
        IDateTimeProvider clock,
        IEditorialHtmlSanitizer sanitizer,
        ILogger<UpdateEditorialArticleCommandHandler> logger,
        IEditorialMediaReferenceValidator? mediaReferences = null)
    {
        _context = context;
        _clock = clock;
        _sanitizer = sanitizer;
        _logger = logger;
        _mediaReferences = mediaReferences;
    }

    public async Task<EditorialArticleResult> Handle(UpdateEditorialArticleCommand request, CancellationToken cancellationToken)
    {
        var article = await _context.EditorialArticles.FirstOrDefaultAsync(a => a.Id == request.ArticleId, cancellationToken);

        // Not-visible and not-existing are the same answer (no id enumeration).
        if (article is null || !EditorialArticleAccess.CanView(request.ActorRoles, request.ActorUserId, article))
        {
            return new EditorialArticleResult(EditorialOutcome.NotFound, null);
        }

        // Role before state: a role that can never edit this article gets
        // Forbidden; a Senior+ editor facing a Retracted/Archived article (content
        // locked, Slice 20b) gets Conflict.
        switch (EditorialArticleAccess.CheckEdit(request.ActorRoles, request.ActorUserId, article))
        {
            case TransitionCheck.Forbidden:
                return new EditorialArticleResult(EditorialOutcome.Forbidden, null);
            case TransitionCheck.InvalidState:
                return new EditorialArticleResult(EditorialOutcome.Conflict, EditorialSupport.ToDto(article));
        }

        // Decision D2: only sanitized HTML is ever stored (see Create). After the
        // NotFound/Forbidden gates, so those answers never depend on the body.
        var body = _sanitizer.Sanitize(request.Body);
        if (_mediaReferences is not null)
        {
            var canonicalBody = await _mediaReferences.ValidateAndCanonicalizeAsync(body, cancellationToken);
            if (canonicalBody is null) return new EditorialArticleResult(EditorialOutcome.InvalidBody, null);
            body = canonicalBody;
        }
        if (!EditorialSupport.IsBodyAcceptable(body))
        {
            return new EditorialArticleResult(EditorialOutcome.InvalidBody, null);
        }

        if (!await EditorialSupport.IsFeaturedVideoValidAsync(
            _context, body, request.FeaturedVideoMediaAssetId, cancellationToken))
        {
            return new EditorialArticleResult(EditorialOutcome.InvalidFeaturedVideo, null);
        }

        if (!await EditorialSupport.IsGeographyValidAsync(_context, request.CountryId, request.CityId, cancellationToken))
        {
            return new EditorialArticleResult(EditorialOutcome.InvalidGeography, null);
        }

        if (article.Status == EditorialArticleStatus.Published && (request.EditReason?.Trim().Length ?? 0) < 5)
        {
            return new EditorialArticleResult(EditorialOutcome.InvalidEditReason, null);
        }

        var requestedDesks = request.PresentationDesks ?? article.PresentationDesks.ToArray();
        var desksChanged = !article.PresentationDesks.SequenceEqual(
            Jusoor.Domain.Editorial.EditorialDesk.Catalog.Keys.Where(requestedDesks.Contains));
        var featuredVideoChanged = article.FeaturedVideoMediaAssetId != request.FeaturedVideoMediaAssetId;
        if (article.Status == EditorialArticleStatus.Published && (desksChanged || featuredVideoChanged))
        {
            return new EditorialArticleResult(EditorialOutcome.Conflict, EditorialSupport.ToDto(article));
        }

        // CoverImageUrl: null = leave unchanged (older clients), "" = remove, otherwise set.
        string? newCover = article.SocialImageUrl;
        if (request.CoverImageUrl is not null)
        {
            newCover = string.IsNullOrWhiteSpace(request.CoverImageUrl) ? null : request.CoverImageUrl.Trim();
            if (newCover is not null && (!Uri.TryCreate(newCover, UriKind.Absolute, out var coverUri) || coverUri.Scheme is not ("https" or "http")))
            {
                return new EditorialArticleResult(EditorialOutcome.InvalidBody, null);
            }
        }
        var coverChanged = newCover != article.SocialImageUrl;

        var contentChanged = article.Title != request.Title
            || article.AuthorName != (string.IsNullOrWhiteSpace(request.AuthorName) ? null : request.AuthorName.Trim())
            || article.Summary != request.Summary
            || article.Body != body
            || article.CountryId != request.CountryId
            || article.CityId != request.CityId
            || desksChanged
            || featuredVideoChanged
            || coverChanged;

        // Decision D4: every edit made while the article is live first captures
        // the version readers are about to lose. The snapshot is built BEFORE the
        // mutation (it copies the current values) but only added to the context
        // once UpdateContent has accepted the new content, and both land in one
        // SaveChanges — an edit can never be saved without its revision row.
        EditorialArticleRevision? revision = null;
        if (article.Status == EditorialArticleStatus.Published && contentChanged)
        {
            var number = await _context.EditorialArticleRevisions
                .CountAsync(r => r.ArticleId == article.Id, cancellationToken) + 1;

            revision = EditorialArticleRevision.CaptureBeforeEdit(
                article, number, request.ActorUserId, request.ActorRoles, request.EditReason, _clock.UtcNow);
        }

        var requiresReapproval = article.Status == EditorialArticleStatus.Approved && contentChanged;
        var previousStatus = article.Status;
        article.UpdateContent(request.Title, request.Summary, body, request.CountryId, request.CityId);
        article.SetAuthorName(request.AuthorName);
        article.SetPresentationDesks(requestedDesks);
        article.SetFeaturedVideoMediaAsset(request.FeaturedVideoMediaAssetId);
        if (coverChanged) article.SetCoverImage(newCover);

        if (requiresReapproval)
        {
            article.RequireReapproval();
            _context.EditorialArticleTransitions.Add(EditorialArticleTransition.Create(
                article.Id, previousStatus, article.Status, request.ActorUserId, request.ActorRoles,
                "content edited after approval", _clock.UtcNow));
        }

        if (revision is not null)
        {
            _context.EditorialArticleRevisions.Add(revision);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("EditorialArticle {ArticleId} updated by {UserId}", article.Id, request.ActorUserId);

        return new EditorialArticleResult(EditorialOutcome.Success, EditorialSupport.ToDto(article));
    }
}
