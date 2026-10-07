using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Jusoor.Application.Editorial;

// Phase 3, Slice 20b (decision Q9) — the four post-publication transitions.
// All four go through EditorialWorkflow.ApplyAsync like the Slice 19 commands, so
// the order of checks (NotFound -> Forbidden -> Conflict), the single SaveChanges
// that writes the article AND its append-only transition row, and the "denied or
// conflicting attempts write nothing" guarantee are shared, not re-implemented.
//
//   Retract            Published -> Retracted   Senior/Managing/EIC   public notice 10-1000 + internal reason (both mandatory)
//   Archive            Published -> Archived    Senior/Managing/EIC   reason optional
//   RestoreFromArchive Archived  -> Published   Senior/Managing/EIC   reason optional
//   ReinstateRetracted Retracted -> Draft       Editor-in-Chief only  reason mandatory
//
// Who may do what, and from which state, lives only in EditorialArticleAccess.

/// <summary>
/// Retracts a published article. <paramref name="PublicNotice"/> is the text readers
/// will see (stored on the article); <paramref name="InternalReason"/> is for the
/// newsroom only (stored on the transition row, never exposed publicly).
/// </summary>
public sealed record RetractEditorialArticleCommand(
    Guid ArticleId, string PublicNotice, string InternalReason, string ActorUserId, IReadOnlyList<string> ActorRoles)
    : IRequest<EditorialArticleResult>;

public sealed record ArchiveEditorialArticleCommand(
    Guid ArticleId, string? Reason, string ActorUserId, IReadOnlyList<string> ActorRoles)
    : IRequest<EditorialArticleResult>;

public sealed record RestoreEditorialArticleFromArchiveCommand(
    Guid ArticleId, string? Reason, string ActorUserId, IReadOnlyList<string> ActorRoles)
    : IRequest<EditorialArticleResult>;

public sealed record ReinstateRetractedEditorialArticleCommand(
    Guid ArticleId, string Reason, string ActorUserId, IReadOnlyList<string> ActorRoles)
    : IRequest<EditorialArticleResult>;

public class RetractEditorialArticleCommandValidator : AbstractValidator<RetractEditorialArticleCommand>
{
    /// <summary>A retraction notice shorter than this cannot say what was wrong and why.</summary>
    public const int PublicNoticeMinLength = 10;

    public RetractEditorialArticleCommandValidator()
    {
        RuleFor(x => x.ArticleId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();

        // Length is judged on the trimmed text, because that is what the domain stores
        // and re-checks: " ok " must not pass here and then fail there as a 500.
        RuleFor(x => x.PublicNotice)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(n => n is not null && n.Trim().Length >= PublicNoticeMinLength)
                .WithMessage($"The public retraction notice must be at least {PublicNoticeMinLength} characters.")
            .MaximumLength(EditorialArticle.RetractionNoticeMaxLength);

        RuleFor(x => x.InternalReason)
            .NotEmpty()
            .MaximumLength(EditorialArticleTransition.ReasonMaxLength);
    }
}

public class ArchiveEditorialArticleCommandValidator : AbstractValidator<ArchiveEditorialArticleCommand>
{
    public ArchiveEditorialArticleCommandValidator()
    {
        RuleFor(x => x.ArticleId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(EditorialArticleTransition.ReasonMaxLength); // optional: null passes
    }
}

public class RestoreEditorialArticleFromArchiveCommandValidator : AbstractValidator<RestoreEditorialArticleFromArchiveCommand>
{
    public RestoreEditorialArticleFromArchiveCommandValidator()
    {
        RuleFor(x => x.ArticleId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(EditorialArticleTransition.ReasonMaxLength); // optional: null passes
    }
}

public class ReinstateRetractedEditorialArticleCommandValidator : AbstractValidator<ReinstateRetractedEditorialArticleCommand>
{
    public ReinstateRetractedEditorialArticleCommandValidator()
    {
        RuleFor(x => x.ArticleId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(EditorialArticleTransition.ReasonMaxLength);
    }
}

public class RetractEditorialArticleCommandHandler : IRequestHandler<RetractEditorialArticleCommand, EditorialArticleResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<RetractEditorialArticleCommandHandler> _logger;

    public RetractEditorialArticleCommandHandler(
        IApplicationDbContext context, IDateTimeProvider clock, ILogger<RetractEditorialArticleCommandHandler> logger)
    {
        _context = context;
        _clock = clock;
        _logger = logger;
    }

    public Task<EditorialArticleResult> Handle(RetractEditorialArticleCommand request, CancellationToken cancellationToken)
        => EditorialWorkflow.ApplyAsync(
            _context, _clock, _logger, EditorialAction.Retract,
            request.ArticleId, request.ActorUserId, request.ActorRoles,
            reason: request.InternalReason, cancellationToken, publicNotice: request.PublicNotice);
}

public class ArchiveEditorialArticleCommandHandler : IRequestHandler<ArchiveEditorialArticleCommand, EditorialArticleResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<ArchiveEditorialArticleCommandHandler> _logger;

    public ArchiveEditorialArticleCommandHandler(
        IApplicationDbContext context, IDateTimeProvider clock, ILogger<ArchiveEditorialArticleCommandHandler> logger)
    {
        _context = context;
        _clock = clock;
        _logger = logger;
    }

    public Task<EditorialArticleResult> Handle(ArchiveEditorialArticleCommand request, CancellationToken cancellationToken)
        => EditorialWorkflow.ApplyAsync(
            _context, _clock, _logger, EditorialAction.Archive,
            request.ArticleId, request.ActorUserId, request.ActorRoles, request.Reason, cancellationToken);
}

public class RestoreEditorialArticleFromArchiveCommandHandler : IRequestHandler<RestoreEditorialArticleFromArchiveCommand, EditorialArticleResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<RestoreEditorialArticleFromArchiveCommandHandler> _logger;

    public RestoreEditorialArticleFromArchiveCommandHandler(
        IApplicationDbContext context, IDateTimeProvider clock, ILogger<RestoreEditorialArticleFromArchiveCommandHandler> logger)
    {
        _context = context;
        _clock = clock;
        _logger = logger;
    }

    public Task<EditorialArticleResult> Handle(RestoreEditorialArticleFromArchiveCommand request, CancellationToken cancellationToken)
        => EditorialWorkflow.ApplyAsync(
            _context, _clock, _logger, EditorialAction.RestoreArchived,
            request.ArticleId, request.ActorUserId, request.ActorRoles, request.Reason, cancellationToken);
}

public class ReinstateRetractedEditorialArticleCommandHandler : IRequestHandler<ReinstateRetractedEditorialArticleCommand, EditorialArticleResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<ReinstateRetractedEditorialArticleCommandHandler> _logger;

    public ReinstateRetractedEditorialArticleCommandHandler(
        IApplicationDbContext context, IDateTimeProvider clock, ILogger<ReinstateRetractedEditorialArticleCommandHandler> logger)
    {
        _context = context;
        _clock = clock;
        _logger = logger;
    }

    public Task<EditorialArticleResult> Handle(ReinstateRetractedEditorialArticleCommand request, CancellationToken cancellationToken)
        => EditorialWorkflow.ApplyAsync(
            _context, _clock, _logger, EditorialAction.ReinstateRetracted,
            request.ArticleId, request.ActorUserId, request.ActorRoles, request.Reason, cancellationToken);
}
