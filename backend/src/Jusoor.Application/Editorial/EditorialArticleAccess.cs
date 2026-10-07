using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;

namespace Jusoor.Application.Editorial;

/// <summary>The workflow actions a user can request on an EditorialArticle.</summary>
public enum EditorialAction
{
    Submit,
    RequestRevision,
    Approve,
    Publish,
    Unpublish,
    /// <summary>Slice 20b (Q9): Published → Retracted, with a public notice.</summary>
    Retract,
    /// <summary>Slice 20b (Q9): Published → Archived.</summary>
    Archive,
    /// <summary>Slice 20b (Q9): Archived → Published.</summary>
    RestoreArchived,
    /// <summary>Slice 20b (Q9): Retracted → Draft. Editor-in-Chief only.</summary>
    ReinstateRetracted,
    SchedulePublication,
    CancelScheduledPublication
}

/// <summary>Outcome of an authorization check on a workflow action, in the order handlers report it: role → state.</summary>
public enum TransitionCheck
{
    Allowed,
    /// <summary>The caller's roles (or ownership) do not permit this action on this article.</summary>
    Forbidden,
    /// <summary>The caller may perform the action in general, but not from the article's current state.</summary>
    InvalidState
}

/// <summary>
/// The single, testable home of the editorial Role×Action×State rules.
/// Source of truth: docs/decisions/PHASE3_EDITORIAL_DECISIONS.md (decision D1,
/// which amends the original matrix in PHASE3_ROLE_ACTION_MATRIX_DRAFT.md).
/// "Own items only" and state rules are enforced here, at the resource layer,
/// never by hiding UI controls; controllers add a coarse role gate on top
/// (named policies) as defense in depth.
///
///   Reporter            create; edit OWN drafts; submit OWN drafts. Never publishes.
///   AiEditor/CopyEditor create/edit/submit OWN drafts; edit ANY article InReview;
///                       request a revision (InReview → Draft). Never publish.
///   Senior/Managing/EIC edit any (also after publication — until the corrections
///                       module, D4); submit any draft; request revision; approve;
///                       publish InReview/Approved; unpublish.
///   Senior/Managing/EIC schedule/cancel publication of Approved articles only.
///   EditorInChief       additionally publishes straight from Draft (direct publishing).
///   SystemAdmin         break-glass only: publish (from any state) and unpublish, no
///                       create/edit. Every transition is recorded with the actor's roles.
///
/// Slice 20b (Q9) post-publication states:
///   Senior/Managing/EIC retract (Published → Retracted), archive (Published →
///                       Archived) and restore (Archived → Published).
///   EditorInChief       alone reinstates a retracted article (Retracted → Draft).
///   SystemAdmin         gets NONE of these four: retracting/archiving is an editorial
///                       judgement; the audited break-glass unpublish stays the emergency tool.
///   Retracted and Archived articles are content-locked (nobody edits them). A public
///   correction can still be attached to an Archived article, never to a Retracted one.
///   Researcher, FactChecker, SeoEditor, CrisisEditor: no access in this module.
/// </summary>
public static class EditorialArticleAccess
{
    // HashSet (instance Contains) rather than arrays: array.Contains binds
    // ambiguously between Enumerable and MemoryExtensions overloads on newer
    // C# language versions when used as a method group.
    private static readonly HashSet<string> SeniorUpRoles = new()
    {
        NewsroomRole.SeniorEditor, NewsroomRole.ManagingEditor, NewsroomRole.EditorInChief
    };

    private static readonly HashSet<string> ReviewerOnlyRoles = new()
    {
        NewsroomRole.AiEditor, NewsroomRole.CopyEditor
    };

    /// <summary>Roles allowed to create articles and reach the edit/submit endpoints.</summary>
    public static readonly IReadOnlyList<string> CreatorRoles = new[] { NewsroomRole.Reporter }
        .Concat(ReviewerOnlyRoles).Concat(SeniorUpRoles).ToArray();

    /// <summary>Roles allowed to reach the request-revision endpoint.</summary>
    public static readonly IReadOnlyList<string> ReviewerRoles = ReviewerOnlyRoles.Concat(SeniorUpRoles).ToArray();

    /// <summary>Roles allowed to reach the approve and publish endpoints (SystemAdmin is break-glass: see PublisherRoles).</summary>
    public static readonly IReadOnlyList<string> ApproverRoles = SeniorUpRoles.ToArray();

    /// <summary>Roles allowed to reach the publish and unpublish endpoints, including SystemAdmin's audited break-glass access.</summary>
    public static readonly IReadOnlyList<string> PublisherRoles = SeniorUpRoles.Concat(new[] { NewsroomRole.SystemAdmin }).ToArray();

    /// <summary>
    /// Roles allowed to issue a public correction/notice (decision D4: Senior,
    /// Managing and Editor-in-Chief only). SystemAdmin is deliberately absent:
    /// issuing one is an editorial judgement, not a break-glass operation.
    /// </summary>
    public static readonly IReadOnlyList<string> CorrectionRoles = SeniorUpRoles.ToArray();

    /// <summary>
    /// Roles allowed to reach the retract, archive and restore-from-archive
    /// endpoints (Slice 20b). SystemAdmin is deliberately absent.
    /// </summary>
    public static readonly IReadOnlyList<string> RetractionRoles = SeniorUpRoles.ToArray();

    /// <summary>Roles allowed to reach the reinstate-retracted endpoint (Slice 20b): Editor-in-Chief only.</summary>
    public static readonly IReadOnlyList<string> ReinstateRoles = new[] { NewsroomRole.EditorInChief };

    /// <summary>Roles allowed to reach the CMS at all.</summary>
    public static readonly IReadOnlyList<string> CmsRoles = CreatorRoles
        .Concat(PublisherRoles).Distinct().ToArray();

    public static bool CanCreate(IReadOnlyCollection<string> roles) => roles.Any(r => CreatorRoles.Contains(r));

    public static bool CanViewAll(IReadOnlyCollection<string> roles)
        => roles.Any(r => ReviewerOnlyRoles.Contains(r) || SeniorUpRoles.Contains(r) || r == NewsroomRole.SystemAdmin);

    public static bool CanView(IReadOnlyCollection<string> roles, string userId, EditorialArticle article)
        => CanViewAll(roles) || (roles.Contains(NewsroomRole.Reporter) && IsOwner(userId, article));

    /// <summary>State-aware content-edit rule (see class summary). True only when <see cref="CheckEdit"/> says Allowed.</summary>
    public static bool CanEdit(IReadOnlyCollection<string> roles, string userId, EditorialArticle article)
        => CheckEdit(roles, userId, article) == TransitionCheck.Allowed;

    /// <summary>
    /// Like <see cref="CanEdit"/> but distinguishes WHY an edit is refused, in
    /// the usual order role → state. Retracted and Archived articles are
    /// content-locked (Slice 20b); Scheduled articles are locked until the
    /// schedule is cancelled (Slice 24). A Senior+ editor, who could normally
    /// edit anything, gets <see cref="TransitionCheck.InvalidState"/>; a role
    /// that could never edit such an article gets <see cref="TransitionCheck.Forbidden"/>.
    /// </summary>
    public static TransitionCheck CheckEdit(IReadOnlyCollection<string> roles, string userId, EditorialArticle article)
    {
        if (article.Status is EditorialArticleStatus.Retracted or EditorialArticleStatus.Archived or EditorialArticleStatus.Scheduled)
        {
            return IsSeniorUp(roles) ? TransitionCheck.InvalidState : TransitionCheck.Forbidden;
        }

        return CanEditContent(roles, userId, article) ? TransitionCheck.Allowed : TransitionCheck.Forbidden;
    }

    private static bool CanEditContent(IReadOnlyCollection<string> roles, string userId, EditorialArticle article)
    {
        // Senior editors and above edit anything (the locked states are handled
        // by CheckEdit before this runs). Editing a Published article changes
        // what the public sees: since Slice 20 (D4) every such edit
        // appends a revision snapshot (see UpdateEditorialArticleCommandHandler),
        // and a public correction note can be issued alongside it.
        if (IsSeniorUp(roles))
        {
            return true;
        }

        return article.Status switch
        {
            EditorialArticleStatus.Draft => IsOwner(userId, article) && CanCreate(roles),
            EditorialArticleStatus.InReview => roles.Any(r => ReviewerOnlyRoles.Contains(r)),
            _ => false
        };
    }

    public static TransitionCheck CheckTransition(
        EditorialAction action, IReadOnlyCollection<string> roles, string userId, EditorialArticle article)
    {
        var status = article.Status;
        var seniorUp = IsSeniorUp(roles);
        var admin = roles.Contains(NewsroomRole.SystemAdmin);
        var reviewerOnly = roles.Any(r => ReviewerOnlyRoles.Contains(r));

        switch (action)
        {
            case EditorialAction.Submit:
                // The owner (any creating role) or an editor on someone's behalf.
                if (!(seniorUp || (IsOwner(userId, article) && CanCreate(roles))))
                {
                    return TransitionCheck.Forbidden;
                }

                return status == EditorialArticleStatus.Draft ? TransitionCheck.Allowed : TransitionCheck.InvalidState;

            case EditorialAction.RequestRevision:
                if (!(seniorUp || reviewerOnly))
                {
                    return TransitionCheck.Forbidden;
                }

                if (status == EditorialArticleStatus.InReview)
                {
                    return TransitionCheck.Allowed;
                }

                // Withdrawing an approval is a senior decision, not a copy-desk one.
                if (status == EditorialArticleStatus.Approved)
                {
                    return seniorUp ? TransitionCheck.Allowed : TransitionCheck.Forbidden;
                }

                return TransitionCheck.InvalidState;

            case EditorialAction.Approve:
                if (!seniorUp)
                {
                    return TransitionCheck.Forbidden;
                }

                if (!roles.Contains(NewsroomRole.EditorInChief) && IsOwner(userId, article))
                {
                    return TransitionCheck.Forbidden;
                }

                return status == EditorialArticleStatus.InReview ? TransitionCheck.Allowed : TransitionCheck.InvalidState;

            case EditorialAction.Publish:
                if (!(seniorUp || admin))
                {
                    return TransitionCheck.Forbidden;
                }

                if (!roles.Contains(NewsroomRole.EditorInChief) && IsOwner(userId, article))
                {
                    return TransitionCheck.Forbidden;
                }

                if (status is EditorialArticleStatus.InReview or EditorialArticleStatus.Approved)
                {
                    return TransitionCheck.Allowed;
                }

                // Straight from Draft skips editorial review: Editor-in-Chief
                // (direct publishing) and SystemAdmin (audited break-glass) only.
                if (status == EditorialArticleStatus.Draft)
                {
                    return roles.Contains(NewsroomRole.EditorInChief) || admin
                        ? TransitionCheck.Allowed
                        : TransitionCheck.InvalidState;
                }

                return TransitionCheck.InvalidState;

            case EditorialAction.SchedulePublication:
                if (!seniorUp) return TransitionCheck.Forbidden;
                if (!roles.Contains(NewsroomRole.EditorInChief) && IsOwner(userId, article))
                    return TransitionCheck.Forbidden;
                return status == EditorialArticleStatus.Approved ? TransitionCheck.Allowed : TransitionCheck.InvalidState;

            case EditorialAction.CancelScheduledPublication:
                if (!seniorUp) return TransitionCheck.Forbidden;
                if (!roles.Contains(NewsroomRole.EditorInChief) && IsOwner(userId, article))
                    return TransitionCheck.Forbidden;
                return status == EditorialArticleStatus.Scheduled ? TransitionCheck.Allowed : TransitionCheck.InvalidState;

            case EditorialAction.Unpublish:
                if (!(seniorUp || admin))
                {
                    return TransitionCheck.Forbidden;
                }

                return status == EditorialArticleStatus.Published ? TransitionCheck.Allowed : TransitionCheck.InvalidState;

            // ----- Slice 20b (Q9). SystemAdmin is deliberately NOT accepted below. -----
            case EditorialAction.Retract:
            case EditorialAction.Archive:
                if (!seniorUp)
                {
                    return TransitionCheck.Forbidden;
                }

                return status == EditorialArticleStatus.Published ? TransitionCheck.Allowed : TransitionCheck.InvalidState;

            case EditorialAction.RestoreArchived:
                if (!seniorUp)
                {
                    return TransitionCheck.Forbidden;
                }

                return status == EditorialArticleStatus.Archived ? TransitionCheck.Allowed : TransitionCheck.InvalidState;

            case EditorialAction.ReinstateRetracted:
                if (!roles.Contains(NewsroomRole.EditorInChief))
                {
                    return TransitionCheck.Forbidden;
                }

                return status == EditorialArticleStatus.Retracted ? TransitionCheck.Allowed : TransitionCheck.InvalidState;

            default:
                return TransitionCheck.Forbidden;
        }
    }

    /// <summary>
    /// Slice 20 (D4): a public correction/notice can be attached only by
    /// Senior/Managing/EIC (Forbidden otherwise) and only to an article that is
    /// currently Published or Archived (InvalidState otherwise — there is no
    /// public text to correct on a draft, and a Retracted article shows only its
    /// retraction notice). Slice 20b added Archived: an archived article is still
    /// readable by URL, so it can still need a correction.
    /// Role is checked before state, as for every action.
    /// </summary>
    public static TransitionCheck CheckCorrection(IReadOnlyCollection<string> roles, EditorialArticle article)
    {
        if (!IsSeniorUp(roles))
        {
            return TransitionCheck.Forbidden;
        }

        return article.Status is EditorialArticleStatus.Published or EditorialArticleStatus.Archived
            ? TransitionCheck.Allowed
            : TransitionCheck.InvalidState;
    }

    private static bool IsSeniorUp(IReadOnlyCollection<string> roles) => roles.Any(r => SeniorUpRoles.Contains(r));

    private static bool IsOwner(string userId, EditorialArticle article)
        => string.Equals(article.OwnerUserId, userId, StringComparison.Ordinal);
}
