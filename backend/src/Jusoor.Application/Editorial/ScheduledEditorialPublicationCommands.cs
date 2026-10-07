using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Jusoor.Application.Editorial;

public sealed record ScheduleEditorialPublicationCommand(
    Guid ArticleId, DateTimeOffset ScheduledPublishAtUtc, string ActorUserId,
    IReadOnlyList<string> ActorRoles, string? Reason = null) : IRequest<EditorialArticleResult>;

public sealed record CancelScheduledEditorialPublicationCommand(
    Guid ArticleId, string ActorUserId, IReadOnlyList<string> ActorRoles,
    string? Reason = null) : IRequest<EditorialArticleResult>;

public sealed class ScheduleEditorialPublicationCommandValidator : AbstractValidator<ScheduleEditorialPublicationCommand>
{
    public ScheduleEditorialPublicationCommandValidator()
    {
        RuleFor(x => x.ArticleId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(Jusoor.Domain.Entities.EditorialArticleTransition.ReasonMaxLength);
    }
}

public sealed class CancelScheduledEditorialPublicationCommandValidator : AbstractValidator<CancelScheduledEditorialPublicationCommand>
{
    public CancelScheduledEditorialPublicationCommandValidator()
    {
        RuleFor(x => x.ArticleId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(Jusoor.Domain.Entities.EditorialArticleTransition.ReasonMaxLength);
    }
}

public sealed class ScheduleEditorialPublicationCommandHandler : IRequestHandler<ScheduleEditorialPublicationCommand, EditorialArticleResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<ScheduleEditorialPublicationCommandHandler> _logger;

    public ScheduleEditorialPublicationCommandHandler(
        IApplicationDbContext context, IDateTimeProvider clock,
        ILogger<ScheduleEditorialPublicationCommandHandler> logger)
        => (_context, _clock, _logger) = (context, clock, logger);

    public Task<EditorialArticleResult> Handle(ScheduleEditorialPublicationCommand request, CancellationToken cancellationToken)
        => EditorialWorkflow.ApplyAsync(
            _context, _clock, _logger, EditorialAction.SchedulePublication,
            request.ArticleId, request.ActorUserId, request.ActorRoles, request.Reason,
            cancellationToken, scheduledPublishAtUtc: request.ScheduledPublishAtUtc);
}

public sealed class CancelScheduledEditorialPublicationCommandHandler
    : IRequestHandler<CancelScheduledEditorialPublicationCommand, EditorialArticleResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<CancelScheduledEditorialPublicationCommandHandler> _logger;

    public CancelScheduledEditorialPublicationCommandHandler(
        IApplicationDbContext context, IDateTimeProvider clock,
        ILogger<CancelScheduledEditorialPublicationCommandHandler> logger)
        => (_context, _clock, _logger) = (context, clock, logger);

    public Task<EditorialArticleResult> Handle(CancelScheduledEditorialPublicationCommand request, CancellationToken cancellationToken)
        => EditorialWorkflow.ApplyAsync(
            _context, _clock, _logger, EditorialAction.CancelScheduledPublication,
            request.ArticleId, request.ActorUserId, request.ActorRoles, request.Reason, cancellationToken);
}
