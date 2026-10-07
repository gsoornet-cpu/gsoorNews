using Jusoor.Domain.Common;
using Jusoor.Domain.Enums;

namespace Jusoor.Domain.Entities;

/// <summary>
/// Append-only record of a newsroom role being granted to or revoked from a
/// user: who did it, to whom, which role, when. Role changes are the most
/// security-sensitive write in the system (they decide who can publish and
/// who can reach Atmaen), so each one leaves a row — the same append-only
/// shape as <see cref="HumanReviewDecision"/>: private setters, a validating
/// factory, no update or delete path.
/// </summary>
public class RoleChangeAudit : BaseEntity
{
    public string ActorUserId { get; private set; } = null!;
    public string TargetUserId { get; private set; } = null!;
    public string Role { get; private set; } = null!;
    public RoleChangeAction Action { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }

    private RoleChangeAudit() { }

    public static RoleChangeAudit Create(
        string actorUserId, string targetUserId, string role, RoleChangeAction action, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            throw new ArgumentException("A role change must record who made it.", nameof(actorUserId));
        }

        if (string.IsNullOrWhiteSpace(targetUserId))
        {
            throw new ArgumentException("A role change must record whose role changed.", nameof(targetUserId));
        }

        if (!NewsroomRole.All.Contains(role))
        {
            throw new ArgumentException($"'{role}' is not a newsroom role.", nameof(role));
        }

        return new RoleChangeAudit
        {
            ActorUserId = actorUserId,
            TargetUserId = targetUserId,
            Role = role,
            Action = action,
            OccurredAtUtc = nowUtc
        };
    }
}
