using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Editorial.Contracts;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jusoor.Application.Editorial;

public sealed record EditorialCorrectionResult(EditorialOutcome Outcome, EditorialCorrectionDto? Correction)
{
    public bool Succeeded => Outcome == EditorialOutcome.Success;
}

/// <summary>
/// Attaches an append-only public note ("تنويه صحفي / تصحيح", decision D4) to a
/// Published article. <see cref="IsMajor"/> shows it above the article text
/// instead of below (open question Q11).
/// </summary>
public sealed record IssueEditorialCorrectionCommand(
    Guid ArticleId,
    CorrectionKind Kind,
    string Note,
    bool IsMajor,
    string ActorUserId,
    IReadOnlyList<string> ActorRoles) : IRequest<EditorialCorrectionResult>;

public class IssueEditorialCorrectionCommandValidator : AbstractValidator<IssueEditorialCorrectionCommand>
{
    public IssueEditorialCorrectionCommandValidator()
    {
        RuleFor(x => x.ArticleId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Kind)
            .Must(kind => kind != CorrectionKind.Notice)
            .WithMessage("Notice is a legacy value and cannot be issued for a new correction.");
        RuleFor(x => x.Note).NotEmpty().MaximumLength(EditorialCorrection.NoteMaxLength);
    }
}

public class IssueEditorialCorrectionCommandHandler : IRequestHandler<IssueEditorialCorrectionCommand, EditorialCorrectionResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<IssueEditorialCorrectionCommandHandler> _logger;

    public IssueEditorialCorrectionCommandHandler(
        IApplicationDbContext context, IDateTimeProvider clock, ILogger<IssueEditorialCorrectionCommandHandler> logger)
    {
        _context = context;
        _clock = clock;
        _logger = logger;
    }

    public async Task<EditorialCorrectionResult> Handle(IssueEditorialCorrectionCommand request, CancellationToken cancellationToken)
    {
        var article = await _context.EditorialArticles.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.ArticleId, cancellationToken);

        // Not-visible and not-existing are the same answer (no id enumeration).
        if (article is null || !EditorialArticleAccess.CanView(request.ActorRoles, request.ActorUserId, article))
        {
            return new EditorialCorrectionResult(EditorialOutcome.NotFound, null);
        }

        switch (EditorialArticleAccess.CheckCorrection(request.ActorRoles, article))
        {
            case TransitionCheck.Forbidden:
                return new EditorialCorrectionResult(EditorialOutcome.Forbidden, null);
            case TransitionCheck.InvalidState:
                return new EditorialCorrectionResult(EditorialOutcome.Conflict, null);
        }

        var correction = EditorialCorrection.Issue(
            article.Id, request.Kind, request.Note, request.IsMajor, request.ActorUserId, request.ActorRoles, _clock.UtcNow);

        _context.EditorialCorrections.Add(correction);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "EditorialArticle {ArticleId} {Kind} issued (major: {IsMajor}) by {UserId}",
            article.Id, correction.Kind, correction.IsMajor, request.ActorUserId);

        return new EditorialCorrectionResult(EditorialOutcome.Success, EditorialSupport.ToDto(correction));
    }
}
