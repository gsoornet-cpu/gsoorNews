using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Editorial;

/// <summary>One published video an editor may place in the homepage hero.</summary>
public sealed record HomeVideoCandidateDto(
    Guid ArticleId, string Title, DateTimeOffset PublishedAtUtc, string? ThumbnailUrl, int? HomeVideoOrder);

/// <summary>All published articles with a ready featured video; selected ones first, in the editor's order.</summary>
public sealed record GetHomeVideoCandidatesQuery : IRequest<IReadOnlyList<HomeVideoCandidateDto>>;

public sealed class GetHomeVideoCandidatesQueryHandler
    : IRequestHandler<GetHomeVideoCandidatesQuery, IReadOnlyList<HomeVideoCandidateDto>>
{
    private readonly IApplicationDbContext _context;
    public GetHomeVideoCandidatesQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<HomeVideoCandidateDto>> Handle(
        GetHomeVideoCandidatesQuery request, CancellationToken cancellationToken)
    {
        var rows = await (from article in _context.EditorialArticles.AsNoTracking()
                          join asset in _context.EditorialMediaAssets.AsNoTracking()
                              on article.FeaturedVideoMediaAssetId equals (Guid?)asset.Id
                          where article.Status == EditorialArticleStatus.Published
                                && article.PublishedAtUtc != null
                                && asset.Kind == EditorialMediaKind.Video
                                && asset.Status == EditorialMediaStatus.Ready
                          select new { article.Id, article.Title, article.PublishedAtUtc, article.SocialImageUrl, article.HomeVideoOrder })
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(r => r.HomeVideoOrder is null ? 1 : 0)
            .ThenBy(r => r.HomeVideoOrder)
            .ThenByDescending(r => r.PublishedAtUtc)
            .Select(r => new HomeVideoCandidateDto(r.Id, r.Title, r.PublishedAtUtc!.Value, r.SocialImageUrl, r.HomeVideoOrder))
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
    public const int MaxVideos = 12;

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

        // Every selected id must be a published article with a ready video — the same rule the public query applies.
        var eligible = await (from article in _context.EditorialArticles.AsNoTracking()
                              join asset in _context.EditorialMediaAssets.AsNoTracking()
                                  on article.FeaturedVideoMediaAssetId equals (Guid?)asset.Id
                              where wanted.Contains(article.Id)
                                    && article.Status == EditorialArticleStatus.Published
                                    && asset.Kind == EditorialMediaKind.Video
                                    && asset.Status == EditorialMediaStatus.Ready
                              select article.Id).ToListAsync(cancellationToken);
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
