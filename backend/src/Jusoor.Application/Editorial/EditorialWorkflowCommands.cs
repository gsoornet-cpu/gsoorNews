using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Jusoor.Application.Editorial;

// Phase 3 — editorial workflow (decision D1). Publish/unpublish and scheduled
// publication live in their own command files; all transitions share EditorialWorkflow.

public sealed record SubmitEditorialArticleCommand(
    Guid ArticleId, string ActorUserId, IReadOnlyList<string> ActorRoles) : IRequest<EditorialArticleResult>;

/// <summary>Sends an article back to its author. The reason is mandatory: the author needs to know what to change.</summary>
public sealed record RequestEditorialRevisionCommand(
    Guid ArticleId, string Reason, string ActorUserId, IReadOnlyList<string> ActorRoles) : IRequest<EditorialArticleResult>;

public sealed record ApproveEditorialArticleCommand(
    Guid ArticleId, string ActorUserId, IReadOnlyList<string> ActorRoles) : IRequest<EditorialArticleResult>;

public class SubmitEditorialArticleCommandValidator : AbstractValidator<SubmitEditorialArticleCommand>
{
    public SubmitEditorialArticleCommandValidator()
    {
        RuleFor(x => x.ArticleId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
    }
}

public class RequestEditorialRevisionCommandValidator : AbstractValidator<RequestEditorialRevisionCommand>
{
    public RequestEditorialRevisionCommandValidator()
    {
        RuleFor(x => x.ArticleId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(Jusoor.Domain.Entities.EditorialArticleTransition.ReasonMaxLength);
    }
}

public class ApproveEditorialArticleCommandValidator : AbstractValidator<ApproveEditorialArticleCommand>
{
    public ApproveEditorialArticleCommandValidator()
    {
        RuleFor(x => x.ArticleId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
    }
}

public class SubmitEditorialArticleCommandHandler : IRequestHandler<SubmitEditorialArticleCommand, EditorialArticleResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<SubmitEditorialArticleCommandHandler> _logger;

    public SubmitEditorialArticleCommandHandler(
        IApplicationDbContext context, IDateTimeProvider clock, ILogger<SubmitEditorialArticleCommandHandler> logger)
    {
        _context = context;
        _clock = clock;
        _logger = logger;
    }

    public Task<EditorialArticleResult> Handle(SubmitEditorialArticleCommand request, CancellationToken cancellationToken)
        => EditorialWorkflow.ApplyAsync(
            _context, _clock, _logger, EditorialAction.Submit,
            request.ArticleId, request.ActorUserId, request.ActorRoles, reason: null, cancellationToken);
}

public class RequestEditorialRevisionCommandHandler : IRequestHandler<RequestEditorialRevisionCommand, EditorialArticleResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<RequestEditorialRevisionCommandHandler> _logger;

    public RequestEditorialRevisionCommandHandler(
        IApplicationDbContext context, IDateTimeProvider clock, ILogger<RequestEditorialRevisionCommandHandler> logger)
    {
        _context = context;
        _clock = clock;
        _logger = logger;
    }

    public Task<EditorialArticleResult> Handle(RequestEditorialRevisionCommand request, CancellationToken cancellationToken)
        => EditorialWorkflow.ApplyAsync(
            _context, _clock, _logger, EditorialAction.RequestRevision,
            request.ArticleId, request.ActorUserId, request.ActorRoles, request.Reason, cancellationToken);
}

public class ApproveEditorialArticleCommandHandler : IRequestHandler<ApproveEditorialArticleCommand, EditorialArticleResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<ApproveEditorialArticleCommandHandler> _logger;

    public ApproveEditorialArticleCommandHandler(
        IApplicationDbContext context, IDateTimeProvider clock, ILogger<ApproveEditorialArticleCommandHandler> logger)
    {
        _context = context;
        _clock = clock;
        _logger = logger;
    }

    public Task<EditorialArticleResult> Handle(ApproveEditorialArticleCommand request, CancellationToken cancellationToken)
        => EditorialWorkflow.ApplyAsync(
            _context, _clock, _logger, EditorialAction.Approve,
            request.ArticleId, request.ActorUserId, request.ActorRoles, reason: null, cancellationToken);
}
