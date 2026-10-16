using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Editorial;

/// <summary>One published uploaded or embedded video an editor may place in the homepage hero.</summary>
public sealed record HomeVideoCandidateDto(
    Guid ArticleId, string Title, DateTimeOffset PublishedAtUtc, string? ThumbnailUrl, int? HomeVideoOrder, bool IsEmbedded);

/// <summary>All published articles with a ready uploaded or approved embedded featured video; selected ones first, in editor order.</summary>
public sealed record GetHomeVideoCandidatesQuery : IRequest<IReadOnlyList<HomeVideoCandidateDto>>;

public sealed class GetHomeVideoCandidatesQueryHandler
    : IRequestHandler<GetHomeVideoCandidatesQuery, IReadOnlyList<HomeVideoCandidateDto>>
{
    private readonly IApplicationDbContext _context;
    public GetHomeVideoCandidatesQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<HomeVideoCandidateDto>> Handle(
        GetHomeVideoCandidatesQuery request, CancellationToken cancellationToken)
    {
        var rows = await _context.EditorialArticles.AsNoTracking()
            .Where(article => article.Status == EditorialArticleStatus.Published
                && article.PublishedAtUtc != null
                && (article.FeaturedVideoEmbedUrl != null || _context.EditorialMediaAssets.Any(asset =>
                    asset.Id == article.FeaturedVideoMediaAssetId
                    && asset.Kind == EditorialMediaKind.Video && asset.Status == EditorialMediaStatus.Ready)))
            .Select(article => new { article.Id, article.Title, article.PublishedAtUtc, article.SocialImageUrl,
                article.HomeVideoOrder, IsEmbedded = article.FeaturedVideoEmbedUrl != null })
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(r => r.HomeVideoOrder is null ? 1 : 0)
            .ThenBy(r => r.HomeVideoOrder)
            .ThenByDescending(r => r.PublishedAtUtc)
            .Select(r => new HomeVideoCandidateDto(r.Id, r.Title, r.PublishedAtUtc!.Value, r.SocialImageUrl, r.HomeVideoOrder, r.IsEmbedded))
            .ToArray();
    }
}

/// <summary>
/// Replaces the whole homepage video selection: <paramref name="ArticleIds"/> in the order given
/// (1..n); every other article is removed from the hero. An empty list clears the hero.
/// </summary>
public sealed record SetHomeVideosCommand(IReadOnlyList<Guid> ArticleIds) : IRequest<SetHomeVideosOutcome>;

public enum SetHomeVideosOutcome { Success, InvalidSelection }

public sealed class SetHomeVideosCommandValidator : AbstractValidator<SetHomeVideosCommand>
{
    public const int MaxVideos = 7;

    public SetHomeVideosCommandValidator()
    {
        RuleFor(x => x.ArticleIds).NotNull()
            .Must(ids => ids.Count <= MaxVideos).WithMessage($"At most {MaxVideos} videos.")
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Duplicate article ids.");
    }
}

public sealed class SetHomeVideosCommandHandler : IRequestHandler<SetHomeVideosCommand, SetHomeVideosOutcome>
{
    private readonly IApplicationDbContext _context;
    public SetHomeVideosCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<SetHomeVideosOutcome> Handle(SetHomeVideosCommand request, CancellationToken cancellationToken)
    {
        var wanted = request.ArticleIds.ToList();

        // Every selected id must be a published article with a ready upload or an approved embed — the public query uses the same rule.
        var eligible = await _context.EditorialArticles.AsNoTracking()
            .Where(article => wanted.Contains(article.Id)
                && article.Status == EditorialArticleStatus.Published
                && (article.FeaturedVideoEmbedUrl != null || _context.EditorialMediaAssets.Any(asset =>
                    asset.Id == article.FeaturedVideoMediaAssetId
                    && asset.Kind == EditorialMediaKind.Video && asset.Status == EditorialMediaStatus.Ready)))
            .Select(article => article.Id)
            .ToListAsync(cancellationToken);
        if (eligible.Count != wanted.Count) return SetHomeVideosOutcome.InvalidSelection;

        var touched = await _context.EditorialArticles
            .Where(a => a.HomeVideoOrder != null || wanted.Contains(a.Id))
            .ToListAsync(cancellationToken);

        foreach (var article in touched)
        {
            var index = wanted.IndexOf(article.Id);
            article.SetHomeVideoOrder(index >= 0 ? index + 1 : null);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return SetHomeVideosOutcome.Success;
    }
}
