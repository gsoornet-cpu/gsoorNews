namespace Jusoor.Domain.Enums;

/// <summary>
/// Workflow state of a newsroom-authored EditorialArticle (Phase 3, Slice 19;
/// product decision D1, docs/decisions/PHASE3_EDITORIAL_DECISIONS.md).
///
///   Draft ──submit──▶ InReview ──approve──▶ Approved ──publish──▶ Published
///     ▲                  │  ▲                    │
///     └──request-revision┘  └────────────────────┘ (revision can also be requested from Approved)
///
///   Published ──unpublish──▶ Draft
///   Published ──retract────▶ Retracted ──reinstate (EIC only)──▶ Draft
///   Published ──archive────▶ Archived  ──restore──────────────▶ Published
///
/// The column is stored as an int, so adding states is additive: Draft=1 and
/// Published=2 keep their original values. Scheduled=5 represents an approved
/// article waiting for its durable Hangfire publication time. Retracted=6 and Archived=7 were
/// added in Slice 20b (decision Q9, PHASE3_EDITORIAL_DECISIONS.md §5.2).
///
/// PUBLIC VISIBILITY: only Published appears in feeds. Archived is readable by
/// URL (and in the main sitemap) but not listed; Retracted shows a notice only,
/// never the body. Any new reader of EditorialArticles must not treat
/// "not Draft" as "public".
/// </summary>
public enum EditorialArticleStatus
{
    Draft = 1,
    Published = 2,
    InReview = 3,
    Approved = 4,
    Scheduled = 5,
    Retracted = 6,
    Archived = 7
}
