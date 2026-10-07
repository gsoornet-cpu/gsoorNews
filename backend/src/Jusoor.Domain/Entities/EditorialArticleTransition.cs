using Jusoor.Domain.Common;
using Jusoor.Domain.Enums;

namespace Jusoor.Domain.Entities;

/// <summary>
/// Append-only record of one workflow transition of an EditorialArticle: who
/// moved it from which state to which, in which capacity, when, and why.
/// Decision D1 requires an immutable audit trail; same shape as
/// <see cref="RoleChangeAudit"/> — private setters, a validating factory, no
/// update or delete path. <see cref="ActorRoles"/> is a snapshot of the roles
/// the actor held at that moment, so a break-glass action (e.g. a SystemAdmin
/// publishing) stays identifiable even after roles change.
/// </summary>
public class EditorialArticleTransition : BaseEntity
{
    public const int ReasonMaxLength = 1000;
    public const int ActorRolesMaxLength = 256;

    public Guid ArticleId { get; private set; }
    public EditorialArticleStatus FromStatus { get; private set; }
    public EditorialArticleStatus ToStatus { get; private set; }
    public string ActorUserId { get; private set; } = null!;
    public string ActorRoles { get; private set; } = null!;
    public string? Reason { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }

    private EditorialArticleTransition() { }

    public static EditorialArticleTransition Create(
        Guid articleId,
        EditorialArticleStatus from,
        EditorialArticleStatus to,
        string actorUserId,
        IEnumerable<string> actorRoles,
        string? reason,
        DateTimeOffset nowUtc)
    {
        if (articleId == Guid.Empty)
        {
            throw new ArgumentException("A transition must reference an article.", nameof(articleId));
        }

        if (from == to)
        {
            throw new ArgumentException("A transition must change the state.", nameof(to));
        }

        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            throw new ArgumentException("A transition must record who made it.", nameof(actorUserId));
        }

        var roles = string.Join(",", actorRoles.Where(r => !string.IsNullOrWhiteSpace(r)).OrderBy(r => r, StringComparer.Ordinal));
        if (roles.Length > ActorRolesMaxLength)
        {
            roles = roles[..ActorRolesMaxLength];
        }

        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (trimmedReason is not null && trimmedReason.Length > ReasonMaxLength)
        {
            throw new ArgumentException($"Reason cannot exceed {ReasonMaxLength} characters.", nameof(reason));
        }

        return new EditorialArticleTransition
        {
            ArticleId = articleId,
            FromStatus = from,
            ToStatus = to,
            ActorUserId = actorUserId,
            ActorRoles = roles,
            Reason = trimmedReason,
            OccurredAtUtc = nowUtc
        };
    }
}
