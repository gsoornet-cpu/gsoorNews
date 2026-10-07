# Phase 3 Role×Action Permission Matrix — Draft for Review

**Status: APPROVED by the developer, with corrections (see "Developer corrections to the draft" below) — this supersedes the original draft matrix wherever the two disagree. Still not wired into `AuthorizationPolicies`/ASP.NET Identity: RBAC enforcement is queued as step 8 of the approved Phase 2/3 execution order (`docs/project-progress.html`), after the Atmaen Case core exists for several of these rules (assigned-Case scoping, break-glass access) to have anything real to enforce against. Several of the actions this matrix covers (article editing, publishing, corrections, SEO) also have no endpoints yet — Phase 3's own CMS work — so those rows describe intended authorization rules for endpoints not yet built, not currently-enforced behavior.**

## Why this document exists

The engineering roadmap lists a written Role×Action permission matrix as
something that must exist **before the first line of Phase 3 Auth/RBAC code**
is written. I inspected the repository and the spec directly:

- `Jusoor.Domain/Enums/NewsroomRole.cs` seeds the 11 role names from spec §25
  as ASP.NET Identity roles, but its own doc comment says explicitly: *"no
  permission matrix is enforced yet... enforcing these against endpoints
  before Phase 3's Role×Action permission matrix exists would mean inventing
  the matrix twice."*
- `Jusoor.Application/Common/Security/AuthorizationPolicies.cs` defines one
  named policy so far (`CanResolveHumanReview`), whose own comment says the
  four roles currently satisfying it (SeniorEditor, ManagingEditor,
  EditorInChief, SystemAdmin) are *"a Phase 1 default, not a spec-mandated
  permission matrix."*
- Spec §25 names the 11 roles and says each has "specific RBAC permissions"
  (`كل دور بصلاحيات RBAC محددة`) but does not itself enumerate the matrix —
  it lists the dashboard's content sections (incoming stories, source
  alerts, dedup clusters, verification queue, AI drafts, human review queue,
  scheduled/published articles, corrections, crisis alerts) without mapping
  which role can do what to which.
- Grep across every controller in the codebase confirms no role-scoped
  `[Authorize(Roles = ...)]` exists anywhere except the interim
  `CanResolveHumanReview` policy described above.

So the prerequisite is genuinely missing, matching what I reported before
starting this task. What follows is a first draft, built from: the spec's own
role list (§25) and dashboard content list (§25), the spec's contributor/
correspondent role list (§14, a related but distinct hierarchy below the
newsroom roles), the case-management actions Atmaen implies (§10, §13), and
the actions this codebase's slices have already implemented or named (source
management, review-queue resolution, publishing).

**This draft is intentionally conservative where the spec is silent** —
several cells below are marked "no spec authority, engineering judgment" or
left as an open question rather than guessed at confidently. Please correct,
reject, or fill in anything marked that way rather than treating the filled
cells as settled just because they're in a table.

---

## Roles in scope

From `NewsroomRole.cs` (spec §25), in ascending seniority as the spec lists
them:

1. **Reporter** (مراسل)
2. **Researcher** (باحث)
3. **AiEditor** (محرّر ذكاء اصطناعي) — human editor who supervises AI-drafted content, not the AI system itself
4. **CopyEditor** (محرّر لغوي)
5. **SeniorEditor** (محرّر أول)
6. **ManagingEditor** (مدير التحرير)
7. **EditorInChief** (رئيس التحرير)
8. **FactChecker** (مدقّق حقائق)
9. **SeoEditor** (محرّر سيو)
10. **CrisisEditor** (محرّر أزمات)
11. **SystemAdmin** (مسؤول نظام)

**Open question:** §14's separate contributor hierarchy (Reader/Tipster →
Verified Contributor → Local Correspondent → Regional Coordinator →
"Newsroom" for final approval) is *not* the same list as §25's 11 roles, and
the spec doesn't state how (or whether) the two hierarchies map onto one
Identity role system versus two separate ones. This draft treats them as
separate for now (contributor roles are a distinct, lower-privilege tier that
feeds *into* the newsroom queue rather than acting within it) — flagged here
rather than merged, since merging them is itself a design decision this
document shouldn't make silently.

---

## Actions in scope

Grouped by the subsystem each already exists (or is planned) in:

**Sourcing & ingestion** (`Jusoor.Api/Controllers/IngestionController.cs`,
currently `[Authorize]` only, comment marks role-scoping as Phase 3 scope)
- Manage sources (add/deactivate a Source)
- Trigger/view ingestion runs

**Editorial pipeline** (Stories/Articles, spec §25's dashboard sections)
- View incoming stories / dedup clusters / verification queue
- Edit an AI-drafted article
- Approve a human-review-queue item (`ReviewController`, currently the one
  real policy, `CanResolveHumanReview`)
- Fact-check a story
- SEO-edit a published or drafted article
- Schedule / publish an article
- Issue a correction
- Trigger/manage a crisis alert (§11 — crisis mode)

**Atmaen case management** (§10, §13 — not yet implemented, Phase 2-blocked;
included here because §25/Phase 3 RBAC is expected to also gate this once it
exists)
- View an assigned case
- View any case (cross-region)
- Update case status (§10's 8 status labels)
- Access case audit log (§22: "full audit log for every Case access")

**Administration**
- Manage newsroom role assignments (who holds which role)
- System/infrastructure configuration

---

## Draft matrix

Legend: **F** = Full access · **O** = Own/assigned items only · **R** =
Read-only · **—** = No access · **?** = No direct spec authority; engineering
judgment, needs your confirmation or correction

| Action | Reporter | Researcher | AiEditor | CopyEditor | SeniorEditor | ManagingEditor | EditorInChief | FactChecker | SeoEditor | CrisisEditor | SystemAdmin |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Manage sources (add/deactivate) | — | ?R | — | — | ?R | F | F | — | — | — | F |
| View incoming stories / dedup clusters | R | F | R | R | F | F | F | R | R | F | F |
| Edit an AI-drafted article | ?— | ?— | F | F | F | F | F | — | ?— | ?— | — |
| Approve human-review-queue item | — | — | — | — | **F**¹ | **F**¹ | **F**¹ | — | — | — | **F**¹ |
| Fact-check a story | — | ?R | — | — | R | R | R | F | — | — | — |
| SEO-edit an article | — | — | — | — | ?R | ?R | ?R | — | F | — | — |
| Schedule / publish an article | — | — | — | ?— | O (own beat?) | F | F | — | — | — | F |
| Issue a correction | — | — | — | — | O | F | F | — | — | — | F |
| Trigger/manage a crisis alert | — | — | — | — | R | F | F | — | — | F | F |
| View assigned Atmaen case | — | — | — | — | — | — | — | — | — | O | R (audit purposes) |
| View any Atmaen case (cross-region) | — | — | — | — | — | ?R | ?R | — | — | F | F |
| Update Atmaen case status | — | — | — | — | — | — | — | — | — | F | — |
| Access Atmaen case audit log | — | — | — | — | — | — | — | — | — | R | F |
| Manage newsroom role assignments | — | — | — | — | — | — | R | — | — | — | F |
| System/infrastructure configuration | — | — | — | — | — | — | — | — | — | — | F |

¹ This row is the **one row with real authority behind it today** —
`CanResolveHumanReview` in `AuthorizationPolicies.cs`/`DependencyInjection.cs`
already grants exactly these four roles this exact action. Everything else in
the table is new proposed content, not yet reflected in code anywhere.

---

## Things this draft deliberately leaves open rather than guessing

1. **Reporter and Researcher's write access.** The spec describes these as
   contributing/investigating roles but never explicitly states whether a
   Reporter can *edit* their own submitted draft after a CopyEditor or
   AiEditor has touched it, or only submit and then hand off. Marked `?—`
   above pending your answer rather than assumed either way.
2. **"Own beat" scoping for SeniorEditor publishing.** Newsrooms commonly
   scope senior editors to a beat/desk, but nothing in the spec establishes
   desks/beats as a concept in this system yet. Marked `?—`.
2b. Related: whether *any* row needs "own items only" (**O**) scoping
   requires a concept of ownership/assignment on `Story`/`Article` that
   doesn't fully exist yet outside of the CrisisEditor/Atmaen case
   assignment discussed in the Phase 2 decision-readiness document
   (decision 3, trusted response network) — several **O** cells above are
   placeholders for that concept, not implementations of it.
3. **Whether SystemAdmin should have blanket F on Atmaen case content.**
   §22 says Atmaen access is "need-to-know" and §13 requires anti-enumeration
   protections even internally. Giving SystemAdmin unrestricted `F` on case
   *content* (not just system configuration) may violate need-to-know in
   spirit even if SystemAdmin technically administers the system it lives
   in. Marked `F` provisionally only because *someone* needs break-glass
   access for support/security incidents, but this specific cell is the one
   most worth challenging.
4. **AiEditor vs. AI system itself.** Confirmed AiEditor is a human role
   (supervises AI-drafted content) per §25's phrase "AI editor... AI
   discovers, classifies, summarizes, suggests; final publication... always
   requires human approval" (§03) — not a service account. No action item,
   just recorded so it isn't re-litigated later.
5. **Contributor-tier roles (§14) are excluded from this matrix entirely**,
   per the open question above — they are not `NewsroomRole` Identity roles
   in the codebase today, and merging the two hierarchies is a decision this
   draft is flagging, not making.

## What this document is not

This is not an implementation. No `[Authorize(Roles=...)]` attribute, no
`AuthorizationPolicies` entry beyond the existing `CanResolveHumanReview`,
and no seed-data change has been made as part of producing this draft.
Per the standing instruction, Phase 3 RBAC implementation should not begin
until a matrix along these lines is reviewed, corrected, and explicitly
approved.

---

## Developer corrections to the draft — superseding content

The developer approved the overall matrix shape and role list but corrected
several specific cells and added rules the original draft either left as `?`
or got wrong. These corrections are the approved direction; where a cell
below differs from the "Draft matrix" table above, **this section wins.**

### Editorial permissions

- **Reporter:** may edit only content the Reporter originally created/
  submitted. No general access to arbitrary AI drafts/articles. This closes
  open question 1 from the original draft — Reporter's `?—` cells on "Edit an
  AI-drafted article" resolve to **O (own submissions only)**, not full
  access and not zero access.
- **Researcher:** read/research access only, confirmed — no general article-
  editing permission at all (closes the Researcher side of open question 1;
  the `?—`/`?R` cells for Researcher on editing resolve to **—**).
- **Source management** (supersedes the draft row): Researcher → R,
  SeniorEditor → R, ManagingEditor → F, EditorInChief → F, SystemAdmin → F,
  all other roles → **—**. This resolves the draft's `?R` cells for
  Researcher/SeniorEditor into confirmed **R**.
- **Fact-checking** (supersedes the draft row): FactChecker → F, SeniorEditor
  → R, ManagingEditor → R, EditorInChief → R, Researcher → R, all other roles
  → **—**. Resolves the draft's `?R` for Researcher into confirmed **R**.
- **SEO** (supersedes the draft row): SeoEditor → F, SeniorEditor → R,
  ManagingEditor → R, EditorInChief → R, all other roles → **—**. Resolves
  the draft's `?—`/`?R` cells into confirmed **R** for the three editor
  roles and **—** for everyone else, including AiEditor.
- **Schedule/Publish** (supersedes the draft row): SeniorEditor →
  **O (own items only)**, ManagingEditor → F, EditorInChief → F, SystemAdmin
  → F, all other roles → **—**. This resolves open question 2 (the draft's
  `?—` for CopyEditor and the "own beat?" uncertainty for SeniorEditor): the
  answer is ownership of the specific item, not a beat/desk concept — **do
  not build a beat/desk model**, and CopyEditor gets **no** publish access.
  Ownership/assignment must be enforced by the authorization/resource layer,
  never hidden as a UI-only restriction.
- **Corrections** (supersedes the draft row): SeniorEditor →
  **O (own items only)**, ManagingEditor → F, EditorInChief → F, SystemAdmin
  → F, all other roles → **—**. Same own-item enforcement rule as
  Schedule/Publish.
- **Crisis alerts** (supersedes the draft row): SeniorEditor → R,
  ManagingEditor → F, EditorInChief → F, CrisisEditor → F, SystemAdmin → F,
  all other roles → **—**. Matches the original draft row exactly — confirmed
  as-is, not changed.

### Atmaen permissions — the most significant corrections

- **View assigned Case:** CrisisEditor → **assigned Cases only**. Other
  newsroom roles → no access unless separately approved. **SystemAdmin does
  NOT get normal Case-content access** — this directly overrides the
  original draft's provisional `R (audit purposes)` cell for SystemAdmin,
  which the draft itself flagged as "the cell most worth challenging." It
  was challenged, and it was wrong as drafted: SystemAdmin's access to Case
  content is break-glass only (see below), not a standing read grant.
- **View all/cross-region Cases:** the original draft's biggest miss.
  **CrisisEditor must NOT get unrestricted cross-region access at all** — the
  approved rule is CrisisEditor → **Full only on assigned Cases**, full stop;
  there is no separate "view other regions" grant for CrisisEditor. This
  replaces the draft's `F` (full, cross-region) for CrisisEditor on this row
  entirely — the draft conflated "assigned-Case full access" with
  "cross-region visibility," and those are not the same permission.
  ManagingEditor and EditorInChief keep the draft's provisional cross-region
  **R**, now confirmed rather than provisional (`?R` → **R**), subject to the
  project's need-to-know and anti-enumeration protections (§13, §22).
  SystemAdmin does **not** get blanket F here either — same break-glass-only
  rule as above.
- **Update Case status:** **CrisisEditor only, and only for Cases assigned to
  that specific CrisisEditor** — confirmed exactly as drafted, with the
  assignment scoping made an explicit, non-negotiable authorization rule
  rather than an implied one: a CrisisEditor must not be able to change the
  status of a Case assigned to a different CrisisEditor.
- **Case audit log** (supersedes the draft row): CrisisEditor → R for
  assigned Cases, ManagingEditor → R, **SystemAdmin → break-glass access
  only, with mandatory audit logging of that access itself** (not a standing
  F as the draft had it), all other roles → **—**. Audit-log access is
  governed by the same need-to-know principle as Case content itself.

### SystemAdmin / Atmaen break-glass access — new rule, not in the original draft

This is the most important correction and applies across every Atmaen row
above: **SystemAdmin must not be implemented as `Full` on Atmaen Case-content
endpoints.** SystemAdmin's relationship to Atmaen data is an explicit
exception path, not ordinary business access:

- No ordinary/unrestricted access to Case content, ever.
- A separate break-glass access path exists for legitimate security/support/
  operational incidents only.
- Every break-glass access must itself be audited: administrator, target
  Case/resource, time, and reason/context, captured through the existing
  audit architecture (the same one `HumanReviewDecision`/`RelevanceResult`
  already establish the append-only pattern for).
- This must be a real, separate authorization path in the implementation —
  not `[Authorize(Roles = "SystemAdmin")]` granting the same access a normal
  endpoint would, dressed up with a comment saying it's for emergencies.

### Contributor hierarchy (§14)

Confirmed as a **separate role hierarchy** from the 11 `NewsroomRole` roles —
the original draft's open question 5 is resolved: do not merge the two into
one Identity role model without a further, separately-approved decision. No
merge is approved by this document.

### Implementation-scope rules that apply to all of the above

These are standing rules for whenever Phase 3 RBAC is actually implemented
(step 8 of the execution order — not yet started):

1. Do not implement any cell of the original draft matrix where this
   corrections section supersedes it.
2. Do not invent permissions for any cell not explicitly approved somewhere
   in this document.
3. "Own items only" / "assigned Cases only" must be enforced at the
   authorization/resource layer (e.g. a resource-based `IAuthorizationHandler`
   checking actual ownership/assignment), never only by hiding a UI control.
4. Prefer centralized, testable policy/resource-based authorization over
   scattering `[Authorize(Roles = ...)]` attributes, where the existing
   architecture supports it more cleanly (`AuthorizationPolicies` already
   establishes the named-policy pattern this should extend, not replace with
   ad-hoc role checks).
5. Keep `CanResolveHumanReview`'s current behavior unless it conflicts with
   this matrix; document the transition explicitly if/when it changes.
6. Required test coverage once implemented (both allow and deny paths):
   Reporter editing another user's content (denied); SeniorEditor publishing
   another user's content (denied); CrisisEditor viewing an unassigned
   Atmaen Case (denied); CrisisEditor changing status on an unassigned Case
   (denied); SystemAdmin normal Case-content access (denied); SystemAdmin
   break-glass access (allowed only through the explicit break-glass path,
   and audited).

None of the above has been implemented yet. This document remains a
specification for step 8 of the approved execution order, not a description
of current system behavior.

---

## Amendment (2026-10-01)

The `EditorialArticle` rows of this matrix (publish, edit, state rules for SeniorEditor, CopyEditor/AiEditor, Reporter, ManagingEditor and SystemAdmin) are **superseded by `PHASE3_EDITORIAL_DECISIONS.md` (decision D1)**. All other rows (human review queue, incoming stories, sources, role administration) are unchanged.
