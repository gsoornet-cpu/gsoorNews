using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jusoor.Application.Review;

public enum ResolveReviewOutcome
{
    Resolved,
    StoryNotFound,
    StoryNotInReview
}

public sealed record ResolveReviewResult(bool Succeeded, ResolveReviewOutcome Outcome, StoryStatus? NewStatus);

/// <summary>
/// Resolves a Story out of NeedsHumanReview — the one mutating action this
/// slice adds. ReviewedByUserId deliberately isn't a constructor parameter
/// supplied by the client: it's populated by the controller from
/// ICurrentUserService (the authenticated principal), never from request
/// body input, so a reviewer can never attribute a decision to someone
/// else. Same reasoning FluentValidation's own docs give for never trusting
/// a client-supplied "actor" field on a security-relevant action.
/// </summary>
public sealed record ResolveReviewCommand(
    Guid StoryId,
    bool IsRelevant,
    string Reasoning,
    string ReviewedByUserId) : IRequest<ResolveReviewResult>;

public class ResolveReviewCommandValidator : AbstractValidator<ResolveReviewCommand>
{
    public ResolveReviewCommandValidator()
    {
        RuleFor(x => x.StoryId).NotEmpty();
        RuleFor(x => x.ReviewedByUserId).NotEmpty();

        // The domain entity itself only requires non-empty (see
        // HumanReviewDecision.Create) — this stricter minimum-length rule
        // lives here, at the application/UX-policy layer, specifically so
        // it can change without a migration if the editorial bar for "what
        // counts as an explanation" turns out wrong in practice.
        RuleFor(x => x.Reasoning)
            .NotEmpty().WithMessage("A reason is required to resolve a review — 'ok' or a blank note isn't an editorial decision.")
            .MinimumLength(10).WithMessage("Reasoning must be at least 10 characters — explain the call, don't just record one.")
            .MaximumLength(2000); // matches HumanReviewDecisionConfiguration's column length
    }
}

public class ResolveReviewCommandHandler : IRequestHandler<ResolveReviewCommand, ResolveReviewResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<ResolveReviewCommandHandler> _logger;

    public ResolveReviewCommandHandler(IApplicationDbContext context, IDateTimeProvider clock, ILogger<ResolveReviewCommandHandler> logger)
    {
        _context = context;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ResolveReviewResult> Handle(ResolveReviewCommand request, CancellationToken cancellationToken)
    {
        var story = await _context.Stories.FirstOrDefaultAsync(s => s.Id == request.StoryId, cancellationToken);

        if (story is null)
        {
            return new ResolveReviewResult(false, ResolveReviewOutcome.StoryNotFound, null);
        }

        if (story.Status != StoryStatus.NeedsHumanReview)
        {
            // Not a bug, not logged as a warning — a perfectly normal race
            // in a multi-editor newsroom: two reviewers open the same queue
            // item, one resolves it first. The second request should get a
            // clear, expected 409, not a scary error.
            return new ResolveReviewResult(false, ResolveReviewOutcome.StoryNotInReview, story.Status);
        }

        var now = _clock.UtcNow;

        // Domain method enforces the same NeedsHumanReview invariant again
        // (defense in depth — see its own comment) and is the ONLY thing
        // allowed to change Story.Status; this handler never sets it
        // directly.
        story.ResolveHumanReview(request.IsRelevant, now);

        var decision = HumanReviewDecision.Create(
            story.Id, request.ReviewedByUserId, request.IsRelevant, request.Reasoning, now);
        _context.HumanReviewDecisions.Add(decision);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Story {StoryId} resolved by human review: IsRelevant={IsRelevant}, ReviewedBy={ReviewedByUserId}",
            story.Id, request.IsRelevant, request.ReviewedByUserId);

        return new ResolveReviewResult(true, ResolveReviewOutcome.Resolved, story.Status);
    }
}
