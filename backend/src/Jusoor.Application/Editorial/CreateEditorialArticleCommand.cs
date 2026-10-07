using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Jusoor.Domain.Editorial;

namespace Jusoor.Application.Editorial;

public sealed record CreateEditorialArticleCommand(
    string Title,
    string? Summary,
    string Body,
    Guid? CountryId,
    Guid? CityId,
    string ActorUserId,
    IReadOnlyList<string> ActorRoles,
    string[]? PresentationDesks = null,
    Guid? FeaturedVideoMediaAssetId = null,
    string? CoverImageUrl = null,
    string? AuthorName = null) : IRequest<EditorialArticleResult>;

public class CreateEditorialArticleCommandValidator : AbstractValidator<CreateEditorialArticleCommand>
{
    public CreateEditorialArticleCommandValidator()
    {
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(EditorialArticle.TitleMaxLength);
        RuleFor(x => x.Summary).MaximumLength(EditorialArticle.SummaryMaxLength);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(EditorialArticle.BodyMaxLength);
        RuleFor(x => x.CoverImageUrl).MaximumLength(2048);
        RuleFor(x => x.AuthorName).MaximumLength(120);
        RuleFor(x => x.CountryId).NotNull().When(x => x.CityId.HasValue)
            .WithMessage("A city cannot be set without its country.");
        RuleFor(x => x.PresentationDesks).Must(desks => desks is null ||
            (desks.Length <= 13 && desks.All(EditorialDesk.IsValid) && desks.Distinct(StringComparer.Ordinal).Count() == desks.Length));
    }
}

public class CreateEditorialArticleCommandHandler : IRequestHandler<CreateEditorialArticleCommand, EditorialArticleResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IEditorialHtmlSanitizer _sanitizer;
    private readonly ILogger<CreateEditorialArticleCommandHandler> _logger;
    private readonly IEditorialMediaReferenceValidator? _mediaReferences;

    public CreateEditorialArticleCommandHandler(
        IApplicationDbContext context, IEditorialHtmlSanitizer sanitizer, ILogger<CreateEditorialArticleCommandHandler> logger,
        IEditorialMediaReferenceValidator? mediaReferences = null)
    {
        _context = context;
        _sanitizer = sanitizer;
        _logger = logger;
        _mediaReferences = mediaReferences;
    }

    public async Task<EditorialArticleResult> Handle(CreateEditorialArticleCommand request, CancellationToken cancellationToken)
    {
        if (!EditorialArticleAccess.CanCreate(request.ActorRoles))
        {
            return new EditorialArticleResult(EditorialOutcome.Forbidden, null);
        }

        // Decision D2: only sanitized HTML is ever stored. Checked after the
        // authorization gate (a forbidden caller learns nothing about the body
        // rules) and before the database round-trip (it is the cheaper check).
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

        var article = EditorialArticle.Create(
            request.Title, request.Summary, body, request.ActorUserId, request.CountryId, request.CityId);
        article.SetPresentationDesks(request.PresentationDesks);
        article.SetAuthorName(request.AuthorName);
        article.SetFeaturedVideoMediaAsset(request.FeaturedVideoMediaAssetId);
        try { article.SetCoverImage(request.CoverImageUrl); }
        catch (ArgumentException) { return new EditorialArticleResult(EditorialOutcome.InvalidBody, null); }

        if (await _context.EditorialArticles.AnyAsync(a => a.Slug == article.Slug, cancellationToken))
        {
            article.UpdateSeo(null, null, $"{article.Slug}-{article.Id.ToString("N")[..8]}", null, null, null, null, false, false);
        }

        _context.EditorialArticles.Add(article);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("EditorialArticle {ArticleId} created as draft by {UserId}", article.Id, request.ActorUserId);

        return new EditorialArticleResult(EditorialOutcome.Success, EditorialSupport.ToDto(article));
    }
}
