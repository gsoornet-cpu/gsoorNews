using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Editorial.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Editorial;

public sealed record EditorialHistoryResult(EditorialOutcome Outcome, EditorialHistoryDto? History)
{
    public bool Succeeded => Outcome == EditorialOutcome.Success;
}

public sealed record EditorialRevisionResult(EditorialOutcome Outcome, EditorialRevisionDto? Revision)
{
    public bool Succeeded => Outcome == EditorialOutcome.Success;
}

/// <summary>
/// Workflow transitions + revisions (metadata only) + corrections of one
/// article, oldest first. Visible to anyone who can view the article itself, so
/// a Reporter sees the history — including the reason an article was returned —
/// of their own articles only.
/// </summary>
public sealed record GetEditorialArticleHistoryQuery(
    Guid ArticleId, string ActorUserId, IReadOnlyList<string> ActorRoles) : IRequest<EditorialHistoryResult>;

public class GetEditorialArticleHistoryQueryValidator : AbstractValidator<GetEditorialArticleHistoryQuery>
{
    public GetEditorialArticleHistoryQueryValidator()
    {
        RuleFor(x => x.ArticleId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
    }
}

public class GetEditorialArticleHistoryQueryHandler : IRequestHandler<GetEditorialArticleHistoryQuery, EditorialHistoryResult>
{
    private readonly IApplicationDbContext _context;

    public GetEditorialArticleHistoryQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<EditorialHistoryResult> Handle(GetEditorialArticleHistoryQuery request, CancellationToken cancellationToken)
    {
        var article = await _context.EditorialArticles.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.ArticleId, cancellationToken);

        if (article is null || !EditorialArticleAccess.CanView(request.ActorRoles, request.ActorUserId, article))
        {
            return new EditorialHistoryResult(EditorialOutcome.NotFound, null);
        }

        var transitions = await _context.EditorialArticleTransitions.AsNoTracking()
            .Where(t => t.ArticleId == article.Id)
            .OrderBy(t => t.OccurredAtUtc)
            .ToListAsync(cancellationToken);

        // Project away Body: a revision can be 100,000 characters and the list needs none of it.
        var revisions = await _context.EditorialArticleRevisions.AsNoTracking()
            .Where(r => r.ArticleId == article.Id)
            .OrderBy(r => r.RevisionNumber)
            .Select(r => new { r.RevisionNumber, r.Title, r.EditedByUserId, r.EditorRoles, r.Reason, r.EditedAtUtc })
            .ToListAsync(cancellationToken);

        var corrections = await _context.EditorialCorrections.AsNoTracking()
            .Where(c => c.ArticleId == article.Id)
            .OrderBy(c => c.IssuedAtUtc)
            .ToListAsync(cancellationToken);

        var history = new EditorialHistoryDto(
            transitions.Select(t => new EditorialTransitionDto(
                t.FromStatus.ToString(), t.ToStatus.ToString(), t.ActorUserId, t.ActorRoles, t.Reason, t.OccurredAtUtc)).ToList(),
            revisions.Select(r => new EditorialRevisionSummaryDto(
                r.RevisionNumber, r.Title, r.EditedByUserId, r.EditorRoles, r.Reason, r.EditedAtUtc)).ToList(),
            corrections.Select(c => EditorialSupport.ToDto(c)).ToList());

        return new EditorialHistoryResult(EditorialOutcome.Success, history);
    }
}

/// <summary>The full snapshot of one revision: the article as it read BEFORE that edit.</summary>
public sealed record GetEditorialArticleRevisionQuery(
    Guid ArticleId, int RevisionNumber, string ActorUserId, IReadOnlyList<string> ActorRoles) : IRequest<EditorialRevisionResult>;

public class GetEditorialArticleRevisionQueryValidator : AbstractValidator<GetEditorialArticleRevisionQuery>
{
    public GetEditorialArticleRevisionQueryValidator()
    {
        RuleFor(x => x.ArticleId).NotEmpty();
        RuleFor(x => x.RevisionNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.ActorUserId).NotEmpty();
    }
}

public class GetEditorialArticleRevisionQueryHandler : IRequestHandler<GetEditorialArticleRevisionQuery, EditorialRevisionResult>
{
    private readonly IApplicationDbContext _context;

    public GetEditorialArticleRevisionQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<EditorialRevisionResult> Handle(GetEditorialArticleRevisionQuery request, CancellationToken cancellationToken)
    {
        var article = await _context.EditorialArticles.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.ArticleId, cancellationToken);

        if (article is null || !EditorialArticleAccess.CanView(request.ActorRoles, request.ActorUserId, article))
        {
            return new EditorialRevisionResult(EditorialOutcome.NotFound, null);
        }

        var revision = await _context.EditorialArticleRevisions.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ArticleId == article.Id && r.RevisionNumber == request.RevisionNumber, cancellationToken);

        return revision is null
            ? new EditorialRevisionResult(EditorialOutcome.NotFound, null)
            : new EditorialRevisionResult(EditorialOutcome.Success, new EditorialRevisionDto(
                revision.RevisionNumber, revision.Title, revision.Summary, ArticleBodyContent.ToHtml(revision.Body, revision.BodyFormat),
                revision.CountryId, revision.CityId,
                revision.EditedByUserId, revision.EditorRoles, revision.Reason, revision.EditedAtUtc)
            { AuthorName = revision.AuthorName });
    }
}
