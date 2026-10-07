using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Atmaen;

public enum UpdateCaseStatusOutcome
{
    Updated,
    CaseNotFound,

    /// <summary>The Case exists but is assigned to a different CrisisEditor
    /// (or to no one). Kept distinct from CaseNotFound internally so it can
    /// be logged/tested precisely — but AtmaenController deliberately maps
    /// both to the same 404, so a CrisisEditor cannot use this endpoint to
    /// probe which Case ids exist outside their own assignments.</summary>
    NotAssignedToYou
}

public sealed record UpdateCaseStatusResult(bool Succeeded, UpdateCaseStatusOutcome Outcome, CaseStatus? NewStatus);

/// <summary>
/// CrisisEditorUserId is populated by the controller from the authenticated
/// principal, never from request body input — same attribution-integrity
/// reasoning as ResolveReviewCommand.ReviewedByUserId. This is also the
/// value the assignment check below compares against, so a caller can never
/// claim to be a different CrisisEditor to pass it.
/// </summary>
public sealed record UpdateCaseStatusCommand(
    Guid CaseId,
    CaseStatus NewStatus,
    string CrisisEditorUserId) : IRequest<UpdateCaseStatusResult>;

public class UpdateCaseStatusCommandValidator : AbstractValidator<UpdateCaseStatusCommand>
{
    public UpdateCaseStatusCommandValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.CrisisEditorUserId).NotEmpty();
        RuleFor(x => x.NewStatus).IsInEnum();
    }
}

public class UpdateCaseStatusCommandHandler : IRequestHandler<UpdateCaseStatusCommand, UpdateCaseStatusResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;

    public UpdateCaseStatusCommandHandler(IApplicationDbContext context, IDateTimeProvider clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<UpdateCaseStatusResult> Handle(UpdateCaseStatusCommand request, CancellationToken cancellationToken)
    {
        var @case = await _context.Cases.FirstOrDefaultAsync(c => c.Id == request.CaseId, cancellationToken);

        if (@case is null)
        {
            return new UpdateCaseStatusResult(false, UpdateCaseStatusOutcome.CaseNotFound, null);
        }

        // The actual authorization boundary for "CrisisEditor may change
        // status only on Cases assigned to them" (approved Phase 3
        // decision) — a real equality check against the authenticated
        // user's own id, in code, not a UI filter. No allowed-transition
        // graph is enforced here (which status may follow which): that
        // workflow rule hasn't been approved, so this slice deliberately
        // doesn't invent one.
        if (!string.Equals(@case.AssignedToCrisisEditorUserId, request.CrisisEditorUserId, StringComparison.Ordinal))
        {
            return new UpdateCaseStatusResult(false, UpdateCaseStatusOutcome.NotAssignedToYou, null);
        }

        @case.UpdateStatus(request.NewStatus, _clock.UtcNow);
        @case.LastModifiedByUserId = request.CrisisEditorUserId;

        await _context.SaveChangesAsync(cancellationToken);

        return new UpdateCaseStatusResult(true, UpdateCaseStatusOutcome.Updated, @case.Status);
    }
}
