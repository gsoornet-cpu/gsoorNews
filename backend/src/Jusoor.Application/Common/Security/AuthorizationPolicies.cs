namespace Jusoor.Application.Common.Security;

/// <summary>
/// Named authorization policy identifiers. Kept here (Application), not in
/// Infrastructure where AddAuthorization actually registers them, so that
/// Api controllers reference a policy by name without depending on
/// Infrastructure directly — the same "Api depends on Application, not on
/// Infrastructure's internals" rule IApplicationDbContext already
/// establishes for persistence.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Gate on ResolveReviewCommand (Application/Review) — see
    /// Infrastructure's DependencyInjection.cs for which roles currently
    /// satisfy it and why that role set is a Phase 1 default, not a
    /// spec-mandated permission matrix.</summary>
    public const string CanResolveHumanReview = "CanResolveHumanReview";

    /// <summary>Gate on the Atmaen Case queue/detail/status endpoints
    /// (Application/Atmaen). CrisisEditor only — explicitly NOT SystemAdmin,
    /// per the approved Phase 3 decision that SystemAdmin must not have
    /// ordinary Case-content access; a separate, audited break-glass path
    /// for SystemAdmin is Phase 3 RBAC scope (execution-order step 8), not
    /// built here. This policy only gates who may call these endpoints at
    /// all — the further "and only Cases assigned to *you*" restriction is
    /// enforced inside each handler against the authenticated user's own id,
    /// not by this policy.</summary>
    public const string CanAccessAtmaenCaseQueue = "CanAccessAtmaenCaseQueue";

    // Phase 3, Slice 15 — CMS. Coarse role gates only; the "own items only"
    // decisions live in EditorialArticleAccess (resource-level).
    public const string CanAccessCms = "CanAccessCms";
    public const string CanEditEditorialSeo = "CanEditEditorialSeo";
    public const string CanManageEditorialTaxonomy = "CanManageEditorialTaxonomy";
    public const string CanCreateEditorialArticle = "CanCreateEditorialArticle";
    public const string CanPublishEditorialArticle = "CanPublishEditorialArticle";

    // Phase 3, Slice 19 — editorial workflow (decision D1). Submitting reuses
    // CanCreateEditorialArticle (same roles); publish/unpublish reuse
    // CanPublishEditorialArticle (whose role set includes SystemAdmin's audited
    // break-glass access; the state rules live in EditorialArticleAccess).
    /// <summary>Request a revision (InReview/Approved → Draft): AiEditor, CopyEditor, Senior, Managing, EIC.</summary>
    public const string CanRequestEditorialRevision = "CanRequestEditorialRevision";

    /// <summary>Approve an article InReview: SeniorEditor, ManagingEditor, EditorInChief.</summary>
    public const string CanApproveEditorialArticle = "CanApproveEditorialArticle";

    // Slice 20 (D4): public corrections/notices — Senior, Managing, Editor-in-Chief.
    public const string CanIssueEditorialCorrection = "CanIssueEditorialCorrection";

    /// <summary>Retract/archive/restore published articles: SeniorEditor, ManagingEditor, EditorInChief.</summary>
    public const string CanManageEditorialPublicationState = "CanManageEditorialPublicationState";

    /// <summary>Reinstate a retracted article to Draft: EditorInChief only.</summary>
    public const string CanReinstateRetractedEditorialArticle = "CanReinstateRetractedEditorialArticle";

    // Phase 3, Slice 17 — remaining approved-matrix rows that have endpoints.
    /// <summary>Incoming (ingested, relevance-approved but unreviewed) stories are internal newsroom
    /// material. Every one of the 11 newsroom roles has at least read access to this row.</summary>
    public const string CanViewIncomingStories = "CanViewIncomingStories";

    /// <summary>Source management / triggering ingestion: ManagingEditor, EditorInChief, SystemAdmin
    /// (Researcher and SeniorEditor are read-only on this row and no read endpoint exists yet).</summary>
    public const string CanManageSources = "CanManageSources";

    /// <summary>View newsroom role assignments: EditorInChief (R) and SystemAdmin (F).</summary>
    public const string CanViewRoleAssignments = "CanViewRoleAssignments";

    /// <summary>Change newsroom role assignments: SystemAdmin only.</summary>
    public const string CanManageRoleAssignments = "CanManageRoleAssignments";
}
