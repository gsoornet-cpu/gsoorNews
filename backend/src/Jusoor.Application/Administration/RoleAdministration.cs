using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Stories.Contracts;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Jusoor.Application.Administration;

/// <summary>
/// Approved matrix row "Manage newsroom role assignments"
/// (docs/decisions/PHASE3_ROLE_ACTION_MATRIX_DRAFT.md): EditorInChief → R,
/// SystemAdmin → F, everyone else → none. The controller applies the same
/// rule as a named policy; the handlers re-check it so the rule holds no
/// matter which entry point reaches them.
/// </summary>
public static class RoleAdministrationAccess
{
    public static bool CanView(IReadOnlyCollection<string> roles)
        => roles.Contains(NewsroomRole.SystemAdmin) || roles.Contains(NewsroomRole.EditorInChief);

    public static bool CanChange(IReadOnlyCollection<string> roles)
        => roles.Contains(NewsroomRole.SystemAdmin);
}

public enum RoleChangeOutcome
{
    Success,
    Unchanged,     // already in the requested state — succeeds without writing a duplicate audit row
    Forbidden,
    NotFound,
    InvalidRole,
    Conflict       // would remove the caller's own SystemAdmin role, or the last SystemAdmin
}

public sealed record RoleChangeResult(RoleChangeOutcome Outcome, NewsroomUserInfo? User, string? Reason = null)
{
    public bool Succeeded => Outcome is RoleChangeOutcome.Success or RoleChangeOutcome.Unchanged;
}

// ---------- list ----------

public sealed record ListNewsroomUsersQuery(
    string? Search, int Page, int PageSize, IReadOnlyList<string> ActorRoles)
    : IRequest<PagedResult<NewsroomUserInfo>?>;

public class ListNewsroomUsersQueryValidator : AbstractValidator<ListNewsroomUsersQuery>
{
    public ListNewsroomUsersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

public class ListNewsroomUsersQueryHandler : IRequestHandler<ListNewsroomUsersQuery, PagedResult<NewsroomUserInfo>?>
{
    private readonly IIdentityService _identity;

    public ListNewsroomUsersQueryHandler(IIdentityService identity) => _identity = identity;

    /// <summary>Returns null when the caller may not view role assignments.</summary>
    public async Task<PagedResult<NewsroomUserInfo>?> Handle(ListNewsroomUsersQuery request, CancellationToken cancellationToken)
    {
        if (!RoleAdministrationAccess.CanView(request.ActorRoles))
        {
            return null;
        }

        var page = await _identity.ListUsersAsync(request.Search, request.Page, request.PageSize, cancellationToken);
        return new PagedResult<NewsroomUserInfo>(page.Items, request.Page, request.PageSize, page.TotalCount);
    }
}

// ---------- grant / revoke ----------

public sealed record GrantRoleCommand(
    string TargetUserId, string Role, string ActorUserId, IReadOnlyList<string> ActorRoles) : IRequest<RoleChangeResult>;

public sealed record RevokeRoleCommand(
    string TargetUserId, string Role, string ActorUserId, IReadOnlyList<string> ActorRoles) : IRequest<RoleChangeResult>;

public class GrantRoleCommandValidator : AbstractValidator<GrantRoleCommand>
{
    public GrantRoleCommandValidator()
    {
        RuleFor(x => x.TargetUserId).NotEmpty().MaximumLength(450);
        RuleFor(x => x.Role).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ActorUserId).NotEmpty();
    }
}

public class RevokeRoleCommandValidator : AbstractValidator<RevokeRoleCommand>
{
    public RevokeRoleCommandValidator()
    {
        RuleFor(x => x.TargetUserId).NotEmpty().MaximumLength(450);
        RuleFor(x => x.Role).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ActorUserId).NotEmpty();
    }
}

public class GrantRoleCommandHandler : IRequestHandler<GrantRoleCommand, RoleChangeResult>
{
    private readonly IIdentityService _identity;
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<GrantRoleCommandHandler> _logger;

    public GrantRoleCommandHandler(
        IIdentityService identity, IApplicationDbContext context, IDateTimeProvider clock, ILogger<GrantRoleCommandHandler> logger)
    {
        _identity = identity;
        _context = context;
        _clock = clock;
        _logger = logger;
    }

    public async Task<RoleChangeResult> Handle(GrantRoleCommand request, CancellationToken cancellationToken)
    {
        if (!RoleAdministrationAccess.CanChange(request.ActorRoles))
        {
            return new RoleChangeResult(RoleChangeOutcome.Forbidden, null);
        }

        // Only the 11 approved newsroom roles can ever be assigned — the
        // request cannot mint an arbitrary role name.
        if (!NewsroomRole.All.Contains(request.Role))
        {
            return new RoleChangeResult(RoleChangeOutcome.InvalidRole, null);
        }

        var user = await _identity.FindUserAsync(request.TargetUserId);
        if (user is null)
        {
            return new RoleChangeResult(RoleChangeOutcome.NotFound, null);
        }

        if (user.Roles.Contains(request.Role))
        {
            return new RoleChangeResult(RoleChangeOutcome.Unchanged, user);
        }

        if (!await _identity.AddToRoleAsync(request.TargetUserId, request.Role))
        {
            return new RoleChangeResult(RoleChangeOutcome.Conflict, user, "The role could not be assigned.");
        }

        _context.RoleChangeAudits.Add(RoleChangeAudit.Create(
            request.ActorUserId, request.TargetUserId, request.Role, RoleChangeAction.Granted, _clock.UtcNow));
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Role {Role} GRANTED to user {TargetUserId} by {ActorUserId}", request.Role, request.TargetUserId, request.ActorUserId);

        return new RoleChangeResult(RoleChangeOutcome.Success, await _identity.FindUserAsync(request.TargetUserId));
    }
}

public class RevokeRoleCommandHandler : IRequestHandler<RevokeRoleCommand, RoleChangeResult>
{
    private readonly IIdentityService _identity;
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<RevokeRoleCommandHandler> _logger;

    public RevokeRoleCommandHandler(
        IIdentityService identity, IApplicationDbContext context, IDateTimeProvider clock, ILogger<RevokeRoleCommandHandler> logger)
    {
        _identity = identity;
        _context = context;
        _clock = clock;
        _logger = logger;
    }

    public async Task<RoleChangeResult> Handle(RevokeRoleCommand request, CancellationToken cancellationToken)
    {
        if (!RoleAdministrationAccess.CanChange(request.ActorRoles))
        {
            return new RoleChangeResult(RoleChangeOutcome.Forbidden, null);
        }

        if (!NewsroomRole.All.Contains(request.Role))
        {
            return new RoleChangeResult(RoleChangeOutcome.InvalidRole, null);
        }

        var user = await _identity.FindUserAsync(request.TargetUserId);
        if (user is null)
        {
            return new RoleChangeResult(RoleChangeOutcome.NotFound, null);
        }

        if (!user.Roles.Contains(request.Role))
        {
            return new RoleChangeResult(RoleChangeOutcome.Unchanged, user);
        }

        if (request.Role == NewsroomRole.SystemAdmin)
        {
            // Two lock-out guards. Neither is about trust in the caller — both
            // stop a mistake (or a race) from leaving nobody able to manage roles.
            if (string.Equals(request.TargetUserId, request.ActorUserId, StringComparison.Ordinal))
            {
                return new RoleChangeResult(RoleChangeOutcome.Conflict, user, "You cannot remove your own SystemAdmin role.");
            }

            var admins = await _identity.GetUserIdsInRoleAsync(NewsroomRole.SystemAdmin);
            if (admins.Count <= 1)
            {
                return new RoleChangeResult(RoleChangeOutcome.Conflict, user, "The last SystemAdmin cannot be removed.");
            }
        }

        if (!await _identity.RemoveFromRoleAsync(request.TargetUserId, request.Role))
        {
            return new RoleChangeResult(RoleChangeOutcome.Conflict, user, "The role could not be removed.");
        }

        _context.RoleChangeAudits.Add(RoleChangeAudit.Create(
            request.ActorUserId, request.TargetUserId, request.Role, RoleChangeAction.Revoked, _clock.UtcNow));
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Role {Role} REVOKED from user {TargetUserId} by {ActorUserId}", request.Role, request.TargetUserId, request.ActorUserId);

        return new RoleChangeResult(RoleChangeOutcome.Success, await _identity.FindUserAsync(request.TargetUserId));
    }
}
