using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Editorial.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Editorial;

public sealed record UpdateEditorialSeoCommand(
    Guid ArticleId, string? SeoTitle, string? SeoDescription, string Slug, string? CanonicalUrl,
    string? SocialTitle, string? SocialDescription, string? SocialImageUrl, bool NoIndex, bool NoFollow,
    Guid? PrimaryCategoryId, string[] SecondaryTags, string? TwitterTitle, string? TwitterDescription, string? TwitterImageUrl)
    : IRequest<EditorialSeoResult>;

public sealed record EditorialSeoResult(EditorialOutcome Outcome, EditorialArticleDto? Article);

public sealed class UpdateEditorialSeoCommandValidator : AbstractValidator<UpdateEditorialSeoCommand>
{
    public UpdateEditorialSeoCommandValidator()
    {
        RuleFor(x => x.ArticleId).NotEmpty();
        RuleFor(x => x.PrimaryCategoryId).NotNull().WithMessage("A primary category is required.");
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(180).Matches("^[\\p{L}\\p{Nd}-]+$");
        RuleFor(x => x.SeoTitle).Length(50, 60).When(x => !string.IsNullOrWhiteSpace(x.SeoTitle));
        RuleFor(x => x.SeoDescription).Length(150, 160).When(x => !string.IsNullOrWhiteSpace(x.SeoDescription));
        RuleFor(x => x.CanonicalUrl).MaximumLength(2048);
        RuleFor(x => x.SocialTitle).MaximumLength(300);
        RuleFor(x => x.SocialDescription).MaximumLength(500);
        RuleFor(x => x.SocialImageUrl).MaximumLength(2048);
        RuleFor(x => x.TwitterTitle).Length(50, 60).When(x => !string.IsNullOrWhiteSpace(x.TwitterTitle));
        RuleFor(x => x.TwitterDescription).Length(150, 160).When(x => !string.IsNullOrWhiteSpace(x.TwitterDescription));
        RuleFor(x => x.TwitterImageUrl).MaximumLength(2048);
        RuleFor(x => x.SecondaryTags).Must(tags => tags is not null && tags.Length <= 12 && tags.All(t => t is not null && t.Trim().Length <= 40));
    }
}

public sealed class UpdateEditorialSeoCommandHandler : IRequestHandler<UpdateEditorialSeoCommand, EditorialSeoResult>
{
    private readonly IApplicationDbContext _context;
    public UpdateEditorialSeoCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<EditorialSeoResult> Handle(UpdateEditorialSeoCommand request, CancellationToken cancellationToken)
    {
        var article = await _context.EditorialArticles.FirstOrDefaultAsync(a => a.Id == request.ArticleId, cancellationToken);
        if (article is null) return new(EditorialOutcome.NotFound, null);
        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await _context.EditorialArticles.AnyAsync(a => a.Id != article.Id && a.Slug == slug, cancellationToken))
            return new(EditorialOutcome.Conflict, EditorialSupport.ToDto(article));
        if (request.PrimaryCategoryId.HasValue && !await _context.EditorialCategories.AnyAsync(
                c => c.Id == request.PrimaryCategoryId.Value && c.IsActive, cancellationToken))
            return new(EditorialOutcome.InvalidBody, EditorialSupport.ToDto(article));
        try
        {
            article.UpdateSeo(request.SeoTitle, request.SeoDescription, slug, request.CanonicalUrl,
                request.SocialTitle, request.SocialDescription, request.SocialImageUrl, request.NoIndex, request.NoFollow,
                request.TwitterTitle, request.TwitterDescription, request.TwitterImageUrl);
            article.UpdateTaxonomy(request.PrimaryCategoryId, request.SecondaryTags);
        }
        catch (ArgumentException)
        {
            return new(EditorialOutcome.InvalidBody, EditorialSupport.ToDto(article));
        }
        await _context.SaveChangesAsync(cancellationToken);
        return new(EditorialOutcome.Success, EditorialSupport.ToDto(article));
    }
}
