using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Jusoor.Application.Editorial;

public sealed record PublishEditorialArticleCommand(
    Guid ArticleId, string ActorUserId, IReadOnlyList<string> ActorRoles, string? Reason = null) : IRequest<EditorialArticleResult>;

public sealed record UnpublishEditorialArticleCommand(
    Guid ArticleId, string ActorUserId, IReadOnlyList<string> ActorRoles, string? Reason = null) : IRequest<EditorialArticleResult>;

public class PublishEditorialArticleCommandValidator : AbstractValidator<PublishEditorialArticleCommand>
{
    public PublishEditorialArticleCommandValidator()
    {
        RuleFor(x => x.ArticleId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(EditorialArticleTransition.ReasonMaxLength);
        RuleFor(x => x.Reason)
            .Must(reason => reason is not null && reason.Trim().Length >= 10)
            .WithMessage("SystemAdmin publish requires a reason of at least 10 characters.")
            .When(x => x.ActorRoles.Contains(Jusoor.Domain.Enums.NewsroomRole.SystemAdmin));
    }
}

public class UnpublishEditorialArticleCommandValidator : AbstractValidator<UnpublishEditorialArticleCommand>
{
    public UnpublishEditorialArticleCommandValidator()
    {
        RuleFor(x => x.ArticleId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(EditorialArticleTransition.ReasonMaxLength);
        RuleFor(x => x.Reason)
            .Must(reason => reason is not null && reason.Trim().Length >= 10)
            .WithMessage("SystemAdmin unpublish requires a reason of at least 10 characters.")
            .When(x => x.ActorRoles.Contains(Jusoor.Domain.Enums.NewsroomRole.SystemAdmin));
    }
}

public class PublishEditorialArticleCommandHandler : IRequestHandler<PublishEditorialArticleCommand, EditorialArticleResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<PublishEditorialArticleCommandHandler> _logger;

    public PublishEditorialArticleCommandHandler(
        IApplicationDbContext context, IDateTimeProvider clock, ILogger<PublishEditorialArticleCommandHandler> logger)
    {
        _context = context;
        _clock = clock;
        _logger = logger;
    }

    // Who may publish, and from which state, is decided in EditorialArticleAccess
    // (D1): InReview/Approved for senior editors and above, Draft only for the
    // Editor-in-Chief and SystemAdmin (audited break-glass).
    public Task<EditorialArticleResult> Handle(PublishEditorialArticleCommand request, CancellationToken cancellationToken)
        => EditorialWorkflow.ApplyAsync(
            _context, _clock, _logger, EditorialAction.Publish,
            request.ArticleId, request.ActorUserId, request.ActorRoles, request.Reason, cancellationToken);
}

public class UnpublishEditorialArticleCommandHandler : IRequestHandler<UnpublishEditorialArticleCommand, EditorialArticleResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<UnpublishEditorialArticleCommandHandler> _logger;

    public UnpublishEditorialArticleCommandHandler(
        IApplicationDbContext context, IDateTimeProvider clock, ILogger<UnpublishEditorialArticleCommandHandler> logger)
    {
        _context = context;
        _clock = clock;
        _logger = logger;
    }

    public Task<EditorialArticleResult> Handle(UnpublishEditorialArticleCommand request, CancellationToken cancellationToken)
        => EditorialWorkflow.ApplyAsync(
            _context, _clock, _logger, EditorialAction.Unpublish,
            request.ArticleId, request.ActorUserId, request.ActorRoles, request.Reason, cancellationToken);
}
