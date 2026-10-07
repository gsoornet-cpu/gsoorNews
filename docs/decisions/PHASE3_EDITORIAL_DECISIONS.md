# Phase 3 — Editorial Depth: approved product decisions (D1–D6)

**Status:** decisions supplied and approved by the developer on 2026-10-01 (message "Architectural Guidance & Product Decisions: Phase 3 / Slice 19+").
**Supersedes, for `EditorialArticle` only:** the Role×Action rows in `PHASE3_ROLE_ACTION_MATRIX_DRAFT.md`. Where the two disagree, **this document wins**. The amendments are listed explicitly below so nothing changes silently.

## 1. Slice plan (dependency order)

| Slice | Content | Decision | Status |
|---|---|---|---|
| 19 | Workflow states + role/state transition rules + immutable transition audit + CMS buttons | D1 (without scheduling) | **verified** (439/439, developer run) |
| 20 | Revision history (snapshot on every edit after first publication) + public Corrections box + history read endpoints / CMS history panel | D4 | implemented, awaiting verification — closes the "senior roles can edit live articles without a trace" gap |
| 20b | `Retracted` / `Archived` states | D4 | verified by developer run (992/992); manual/target-database checks remain |
| 21 | Rich text: sanitized HTML storage, server-side sanitizer, WYSIWYG editor (Tiptap), RTL, counters; migration of existing plain-text bodies | D2 | implemented — awaiting verification (restore, migration, build, test; see Q15–Q17 for the defaults it used) |
| 22 | Media library on Supabase Storage (+ editor integration, captions, credits, alt text, focal point) | D3 | verified by developer run (1017/1017); external bucket/CORS checks remain |
| 23 | SEO module (title/description overrides with counters, slug, canonical, OG/Twitter, noindex/nofollow, category + tags, JSON-LD enrichment) | D6 | **verified** (1023/1023, developer run) |
| 24 | Scheduled publishing (`Scheduled` state, `ScheduledPublishAtUtc`, Hangfire job) | D1 sub-slice | implementation delivered; awaiting developer verification |
| — | Story → article promotion / AI drafts | D5 | **intentionally not built**: all publishing is manual; ingestion stays decoupled |

## 2. D1 — Workflow (implemented in Slice 19)

States: `Draft(1) → InReview(3) → Approved(4) → Published(2)`, or `Approved(4) → Scheduled(5) → Published(2)` when scheduled. A scheduled item may return to Approved by cancellation. Persisted as int; existing values never change. `Scheduled(5)` is defined in Slice 24; `Retracted(6)` and `Archived(7)` are defined in Slice 20b.

| Action | Allowed from | Roles |
|---|---|---|
| Submit | Draft | the article's owner (Reporter, AiEditor, CopyEditor, Senior, Managing, EIC) or Senior/Managing/EIC on anyone's draft |
| Request revision (→ Draft, **reason mandatory**, ≤1000 chars) | InReview | AiEditor, CopyEditor, Senior, Managing, EIC |
| Request revision | Approved | Senior, Managing, EIC (withdrawing an approval is a senior decision) |
| Approve | InReview | Senior, Managing, EIC |
| Publish | InReview, Approved | Senior, Managing, EIC, SystemAdmin (break-glass) |
| Publish | **Draft** | **EIC and SystemAdmin only** (direct publishing / break-glass) |
| Unpublish (→ Draft) | Published | Senior, Managing, EIC, SystemAdmin (break-glass) |

Edit rules: Reporter/AiEditor/CopyEditor edit their **own Drafts**; AiEditor/CopyEditor additionally edit **any InReview** article; Senior/Managing/EIC edit any article in any state (including Published — see Q4); SystemAdmin never edits content. A Reporter's submitted article is locked until revised or reviewed.

Visibility: Reporter sees only own articles; AiEditor, CopyEditor, Senior, Managing, EIC, SystemAdmin see all. Researcher, FactChecker, SeoEditor, CrisisEditor have no access to this module. **The public sees `Published` only** — every public query keeps the single rule `Status == Published`.

Audit: every successful transition appends an `EditorialArticleTransition` row (article, from, to, actor id, **snapshot of the actor's roles**, reason, UTC time) in the same save as the state change. No update/delete path. Denied and conflicting attempts write nothing.

### Amendments to the earlier approved matrix (all deliberate, all from D1)
1. **SeniorEditor** publishes **any** InReview/Approved article (was: own items only). Also approves and unpublishes any.
2. **CopyEditor / AiEditor** edit **InReview** articles only (was: edit any article in any state).
3. **Reporter** edits own articles only while **Draft** (was: any state except Published).
4. **Managing editor / Senior** can no longer publish a Draft directly; they go through review. EIC keeps direct publishing.
5. **SystemAdmin** keeps publish/unpublish (from the earlier matrix) but, per D1 "no routine editorial intervention without audit logging", every such action is recorded with the actor's role snapshot. Still cannot create or edit.

## 3. Decisions for later slices (recorded now so they are not re-litigated)

**D2 Rich text** *(implemented in Slice 21)*. Store clean HTML; sanitize **server-side** with an allow-list sanitizer (HtmlSanitizer / Ganss.Xss) — never trust the editor. Editor: Tiptap (ProseMirror), RTL-native, with the toolbar listed in D2, plus word/char/reading-time counters. Links get `rel="noopener noreferrer"`.
**D3 Media.** Supabase Storage. Images WEBP/JPEG/PNG/AVIF/SVG ≤10 MB; video MP4/WEBM ≤100 MB (larger = external embed); PDF ≤20 MB. Mandatory `alt_text` and `credit`; optional `caption`; focal point for thumbnails.
**D4 Corrections.** Append-only "تنويه صحفي / تصحيح" box (timestamp, nature, editor's note) at the bottom, or top for major errors; append-only `ArticleRevisionHistory` (snapshot, author, time, reason) on every edit after first publication. Only **Senior, Managing, EIC** issue corrections/retractions.
**D5.** Manual content entry only.
**D6 SEO.** `seo_title` (50–60 counter), `seo_description` (150–160), custom `slug` (URL-safe, localized, unique), `canonical_url`, OG/Twitter title/description/image, `noindex`/`nofollow`, primary category + secondary tags; editable by SeoEditor, SeniorEditor and above. JSON-LD NewsArticle enrichment (Slice 18 already emits headline, dates, author, publisher, mainEntityOfPage).

## 4. Open questions — none block Slice 19; each has the default currently implemented/planned

| # | Question | Default used |
|---|---|---|
| Q1 | Four-eyes: may someone approve/publish an article they authored (Senior/Managing/EIC)? | **Allowed** (nothing invented). Recommended: forbid self-approval for Senior/Managing; EIC exempt. One-line change in `EditorialArticleAccess` when decided |
| Q2 | Should SystemAdmin break-glass publish/unpublish require a written reason? | Not required; the role snapshot makes it identifiable |
| Q3 | Should editing an **Approved** article send it back to InReview (so approval always matches the text)? | No automatic reset |
| Q4 | Block editing **Published** articles until Slice 20 (revision history), or allow? | **Allowed** for Senior/Managing/EIC (the existing behaviour); not yet versioned |
| Q5 | Article URL once slugs exist: `/article/{id}/{slug}` (id stays the key; old links keep working; slug change = redirect) vs slug-only | Recommend `/article/{id}/{slug}` |
| Q6 | SVG uploads can carry scripts. Reject SVG until a server-side SVG sanitizer is in place, or sanitize on upload? | Recommend: sanitize on upload, serve with `Content-Security-Policy: sandbox`/`Content-Disposition`; until then reject |
| Q7 | Which social/video embed providers are allowed? (sanitizer must allow-list iframe hosts) | Recommend YouTube, Vimeo, X, Instagram, Facebook only |
| Q8 | Supabase: project URL, bucket names, public vs signed URLs; the service-role key stays **server-side only** (never in the browser or `NEXT_PUBLIC_*`); large videos should upload directly via signed URLs, not through the API | Needed before Slice 22 |
| Q9 | `Retracted` vs `Archived`: Retracted = taken down for error (public notice stays at the URL, 410/notice page); Archived = removed from feeds but still readable? | Assumed as stated |
| Q10 | The primary-category list (who defines the taxonomy?) | Needed before Slice 23 |
| Q11 | "Major error" correction placement: chosen by the issuing editor via a `Major` flag | Assumed |
| Q12 | Must an edit of a **Published** article carry a written reason? | **Optional** (`editReason`, ≤1000 chars, stored with the revision). D4 lists a reason in the revision record but does not say it is mandatory; making it mandatory changes the existing PUT contract |
| Q13 | Should issuing a correction advance the public "last updated" / `dateModified`? | **No** (the article row is untouched, so the existing rule — later of publication and last edit — is unchanged) |
| Q14 | Are "تصحيح" (Correction) and "تنويه" (Notice) the only kinds? | **Yes**, assumed from the label "تنويه صحفي / تصحيح"; the enum is an int column, so a kind can be appended later |
| Q15 | Editor scope: Slice 21 offers bold, italic, underline, H2, H3, quote, bullet/numbered lists, horizontal rule, links, undo/redo. **Tables** (listed in the Slice 21 plan) and **images/embeds** are not included — images/embeds belong to Slice 22 (they need Q6–Q8 and mandatory alt text/credit per D3). Do you want tables in the editor? | **No tables yet.** Adding them means one more Tiptap extension plus `table/thead/tbody/tr/th/td` (+ `colspan`/`rowspan`) in the server allow-list |
| Q16 | Existing plain-text bodies: convert lazily (stored rows keep `BodyFormat = PlainText`, are HTML-encoded into paragraphs on every read, and become sanitized HTML the next time an editor saves them) or run a one-time batch conversion? | **Lazy.** No data is rewritten by the migration; revision snapshots keep the format readers actually saw |
| Q17 | Content-Security-Policy for the public site. The article page now renders server-sanitized HTML as markup (`dangerouslySetInnerHTML`); a CSP (no inline script, `script-src 'self'`) would make an XSS-sanitizer bypass far less harmful | **Not set** (the site sets no security headers today). Recommend scheduling it before launch; it is independent of Slice 21 |

*Slice 20 notes:* a revision stores the content **as it read before** the edit (the current version is the article itself); corrections are append-only and cannot be withdrawn — a wrong one is answered by a newer one; SystemAdmin cannot issue corrections (D4: Senior, Managing, Editor-in-Chief only).


## 5.7 Slice 24 — scheduled publication handoff

**Implementation status:** delivered; awaiting developer build, migration, and test verification.

- Only an Approved article can be scheduled. This prevents scheduled publication from bypassing D1 review. Scheduling/cancellation is available to SeniorEditor, ManagingEditor, and EditorInChief; SystemAdmin remains limited to D1's explicit audited publish/unpublish break-glass actions.
- ScheduledPublishAtUtc stores the UTC instant. Scheduling requires a future instant; the CMS accepts local device time and sends its UTC equivalent.
- Cancellation is an audited Scheduled → Approved transition and clears the due time. Scheduled content is read-only until cancellation; schedule it again after any change.
- Hangfire runs a persistent one-minute due-publication sweep. The article row is the durable schedule, so restart/retry cannot lose it. The workflow status is an EF concurrency token; article transition and audit row save atomically. A conflicting cancel or duplicate worker cannot publish twice.
- Automated publication records the actor as system:scheduled-publisher with role marker ScheduledPublisher; public visibility still requires exactly Published, so Scheduled articles remain absent from feeds, detail, sitemap and JSON-LD until the job succeeds.
- Migration: AddScheduledEditorialPublishing adds the nullable due timestamp, due-work index, and a database constraint requiring a due time if and only if status is Scheduled.
