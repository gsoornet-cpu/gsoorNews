# Phase 3 — Editorial Depth: approved product decisions (D1–D6)

**Status:** decisions supplied and approved by the developer on 2026-10-01 (message "Architectural Guidance & Product Decisions: Phase 3 / Slice 19+").
**Supersedes, for `EditorialArticle` only:** the Role×Action rows in `PHASE3_ROLE_ACTION_MATRIX_DRAFT.md`. Where the two disagree, **this document wins**. The amendments are listed explicitly below so nothing changes silently.

## 1. Slice plan (dependency order)

| Slice | Content | Decision | Status |
|---|---|---|---|
| 19 | Workflow states + role/state transition rules + immutable transition audit + CMS buttons | D1 (without scheduling) | **verified** (439/439, developer run) |
| 20 | Revision history (snapshot on every edit after first publication) + public Corrections box + history read endpoints / CMS history panel | D4 | implemented, awaiting verification — closes the "senior roles can edit live articles without a trace" gap |
| 20b | `Retracted` / `Archived` states | D4 + Q9 | **planned, Q9 answered** — see `SLICE_20B_IMPLEMENTATION_PLAN.md` |
| 20c | Workflow & corrections alignment: Q1 (no self-approval), Q2 (break-glass reason), Q3 (edit resets approval), Q12 (mandatory edit reason), Q13 (`dateModified` after a correction), Q14 (four correction kinds) | Q1–Q3, Q12–Q14 | **verified** by developer run (1001/1001 tests; build succeeded) |
| 21 | Rich text: sanitized HTML storage, server-side sanitizer, WYSIWYG editor (Tiptap), RTL, counters; migration of existing plain-text bodies | D2 | **verified** (571/571, build OK, developer run); manual CMS pass and applying the migration to the real DB pending |
| 21b | Simple tables in the editor (Q15) | D2 + Q15 | **verified** by developer run (1005/1005 tests; build succeeded) |
| 22 | Media library on Supabase Storage (+ editor integration, captions, credits, alt text, focal point) | D3 | **verified by developer run** — 1017/1017 tests, build succeeded; browser and live Storage/CORS checks remain |
| 23 | SEO module (title/description overrides with counters, slug, canonical, OG/Twitter, noindex/nofollow, category + tags, JSON-LD enrichment) | D6 | planned — depends on Q5 and Q10 |
| 24 | Scheduled publishing (`Scheduled` state, `ScheduledPublishAtUtc`, Hangfire job) | D1 sub-slice | planned |
| H | Security hardening: Content-Security-Policy (report-only first, then enforce) and other response headers | Q17 | planned — must be done before any public staging/launch |
| — | Story → article promotion / AI drafts | D5 | **intentionally not built**: all publishing is manual; ingestion stays decoupled |

## 2. D1 — Workflow (implemented in Slice 19)

States: `Draft(1) → InReview(3) → Approved(4) → Published(2)`. Persisted as int; existing values never change. `Scheduled`, `Retracted`, `Archived` are reserved numbers (5, 6, 7) and are **not defined** until their slice (an unreachable state is dead code).

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

## 4. Original open questions and the defaults used at the time (answered on 2026-10-02 — see section 5; where they differ, **section 5 wins**)

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

## 5. Decisions on Q1–Q17 (2026-10-02)

**Status:** the developer delegated the editorial/newsroom-workflow decisions ("act as a Senior Journalist and Newsroom Domain Expert … make the executive call") and approved this set on 2026-10-02. Q8 is infrastructure, not editorial; the developer supplied the values (see 5.4).

### 5.1 Decision table

| # | Decision | Where it is built |
|---|---|---|
| Q1 | **Four-eyes.** Senior and Managing editors may **not** approve or publish an article they own/authored. The Editor-in-Chief is exempt (the action stays audited). | 20c |
| Q2 | **Break-glass needs a reason.** A SystemAdmin publish or unpublish requires a written reason (≥10 characters, ≤1000), stored on the transition row. | 20c |
| Q3 | **Approval must match the text.** Any content edit of an `Approved` article returns it to `InReview`, recorded as an automatic transition (reason: "content edited after approval"). | 20c |
| Q4 | Already settled in practice: editing a published article is allowed **and audited** (Slice 20); with Q12 it now also needs a reason. | — |
| Q5 | Article URL is `/article/{id}/{slug}`. The id is the key; old links keep working; a slug change answers 301 to the current slug; canonical always points at the current slug. | 23 |
| Q6 | **SVG uploads are rejected** until a server-side SVG sanitizer exists. Raster formats only (WEBP/JPEG/PNG/AVIF). *Amends D3.* | 22 |
| Q7 | Embed allow-list: YouTube (no-cookie domain), Vimeo, X, Instagram, Facebook, **TikTok**. Enforced by a fixed server-side allow-list and canonical URL patterns; iframes sandboxed and lazy-loaded; anything else is stripped. | 22 |
| Q8 | Values received — see 5.4. Public read for published media, signed upload URLs; confirmed by developer at Slice 22 start. | 22 |
| Q9 | See 5.2. | 20b |
| Q10 | The **Editor-in-Chief owns the taxonomy**. Categories live in a managed table (not an enum); about ten primary categories; country is a separate dimension, not a category. Starter set: أخبار مصر · أخبار المغتربين · إقامة وتأشيرات وقانون · اقتصاد وتحويلات · عمل وتعليم · صحة وأسرة · سفر وطيران · ثقافة ومجتمع · رياضة · تكنولوجيا. Reconciled against `فاينال ديمو/category.html` and `data.js` before Slice 23: treat the demo's curated sections (`mughtarib`, `success`, `egypt`, `official`, `events`, `red`, `lamma`, `secondgen`, `sports`, `arts`, `articles`, `various`) as presentation desks/collections, not all as topical categories. Keep the ten topical categories above as the article's managed primary taxonomy; retain country as its own dimension. Existing demo story labels such as إقامة، فعاليات، تعليم، اطمّن، سفارات، فرص عمل، رياضة، اقتصاد، مقالات رأي inform secondary tags and desk mappings, not duplicate primary categories. Slice 23 must expose CMS-managed category/tag assignment while preserving those demo section URLs/semantics. | 23 |
| Q11 | Confirmed: the issuing editor sets the `Major` flag; major corrections render at the top. | done (20) |
| Q12 | The edit reason is **mandatory** when editing a `Published` article (≥5 characters, ≤1000). The PUT contract and its tests change; acceptable because nothing is live yet. | 20c |
| Q13 | A correction **advances** the public "last updated" / `dateModified`: latest of publication, last edit and latest correction, computed without touching the article row. `datePublished` never changes. | 20c |
| Q14 | Four kinds, not two: **تصحيح** Correction · **توضيح** Clarification · **تنويه المحرر** Editor's note · **تحديث** Update (developing stories — must not read as an error). Appended to the int enum. | 20c |

**Q14 compatibility note (implemented in Slice 20c):** the historical `Notice = 2` enum value remains unchanged so existing database rows still deserialize and display. It is legacy-read-only: the API and application validator reject new Notice corrections, and the CMS offers only the four approved kinds. No enum renumbering or data migration is performed.
| Q15 | Tables: **yes, later.** Simple header-row tables only (no merged cells, no nesting), horizontally scrollable on mobile; allow-list gains `table/thead/tbody/tr/th/td` without `colspan`/`rowspan`. | 21b |
| Q16 | Confirmed: lazy conversion of old bodies (as implemented). | done (21) |
| Q17 | Yes. A dedicated hardening slice sets a Content-Security-Policy (report-only first, then enforce) before any public staging or launch. | H |

### 5.2 Q9 — Retracted vs Archived (the contract for Slice 20b)

**Unpublish vs Retract (newsroom policy).** *Unpublish* is for premature or accidental publication that readers effectively never saw; it returns the article to Draft. *Retract* is for anything readers saw that was wrong, harmful or legally untenable; it leaves a public trace.

| | **Retracted** (6) | **Archived** (7) |
|---|---|---|
| Meaning | Taken down for error | Old and accurate, no longer promoted |
| Allowed from | `Published` only | `Published` only |
| Who | Senior, Managing, EIC | Senior, Managing, EIC |
| Public URL | Notice page. The API answers **HTTP 410**; the body is never sent | Same URL, **HTTP 200**, full article |
| Notice | Mandatory public notice (10–1000 characters) plus an internal reason | Optional reason |
| Indexing | `noindex`; out of both sitemaps | Indexable; **in** the main sitemap, **out** of the news sitemap |
| Feeds / home / category / related / most-read | Excluded | Excluded |
| Site search (when it exists) | Excluded | Included |
| Corrections box | Not shown (the retraction notice replaces it; history kept internally) | Shown; further corrections may be issued |
| Content editing | Locked | Locked (restore to Published first) |
| Way back | **EIC only**: Retracted → Draft, reason mandatory. It never silently republishes | Senior, Managing, EIC: Archived → Published, reason optional |
| SystemAdmin | No retract/archive. The existing audited break-glass unpublish covers an urgent legal takedown | Same |

Auto-archiving is not built; archiving is a manual editorial act.

### 5.3 Amendments to earlier decisions
1. **D1 / D4:** `Retracted` (6) and `Archived` (7) are defined only in Slice 20b; `Scheduled` (5) stays reserved.
2. **D1 matrix:** four new actions (Retract, Archive, RestoreArchived, ReinstateRetracted) — see 5.2 for roles and states.
3. **D3:** SVG removed from the accepted image types until a sanitizer exists (Q6); TikTok added to the embed list (Q7).
4. **Public visibility rule:** list/feed queries keep the single rule `Status == Published`. The detail query becomes Published or Archived (200), Retracted (410 notice). The main sitemap includes Archived; the news sitemap does not. This replaces the rule "the public sees Published only" in section 2 for the detail view and the sitemap.
5. **Slice 20 behaviour changes** (Q12, Q13, Q14) and **Slice 19 behaviour changes** (Q1, Q2, Q3) are scheduled in Slice 20c.

### 5.4 Q8 — Supabase (received 2026-10-02)
- Bucket: `jusoor-media`
- Project URL: `https://ovyllgcgmmnzvjletgyb.supabase.co`
- Service-role key: **intentionally not recorded in this repository or in any document.** It is injected only through the local `.env` (gitignored) into the API container, and it stays server-side: never in the browser, never in a `NEXT_PUBLIC_*` variable. Variable names: `SUPABASE_URL`, `SUPABASE_STORAGE_BUCKET`, `SUPABASE_SERVICE_ROLE_KEY`.
- Access model confirmed by the developer at Slice 22 start: public read for published media; short-lived signed upload URLs created only by the authenticated backend. Large files upload directly from the browser to Supabase Storage, never through the API. The service-role key stays exclusively server-side.

### 5.5 Slice 22 implementation and operations
- The API stores an upload reservation before issuing a signed URL. Only the uploading user can finalize it. Finalization checks object size, MIME type and file signature before the asset becomes selectable; rejected objects are deleted. Pending reservations expire after two hours.
- Allowed uploads: JPEG/PNG/WebP/AVIF ≤10 MiB, MP4/WebM ≤100 MiB, PDF ≤20 MiB. SVG/GIF and mismatched extension/MIME pairs are rejected. Image alt text and credit are mandatory; captions and 0–100% paired focal coordinates are optional.
- Create the `jusoor-media` bucket as **public-read** in Supabase Storage, set its maximum object size to at least 100 MiB, and restrict its allowed MIME types to the list above. Configure Storage CORS for the deployed CMS origin and the methods/headers required by signed PUT and Tus uploads (`PUT`, `POST`, `HEAD`, `PATCH`, `OPTIONS`; `Content-Type`, `Tus-Resumable`, `Upload-Length`, `Upload-Offset`, `Upload-Metadata`, and `x-signature`). Do not expose the service key or enable browser writes with an unrestricted policy.
- Public buckets make each uploaded object's public URL readable before an article is published. Upload only media cleared for public release; the API does not claim private draft-media access control under this Q8 decision.
- Apply the `AddEditorialMediaAssets` EF migration before enabling the media endpoints. Configure only the three server variables listed in 5.4; keep the service-role key in the API secret store, never in the frontend environment.
- Article image and file links are canonicalized from ready media IDs; client URLs and image text are not trusted. Embed URLs are normalized to YouTube no-cookie, Vimeo, X, Instagram, Facebook video/post and TikTok embed paths, and rendered with a restrictive sandbox, lazy loading and referrer policy.
- Slice 22 remains awaiting the developer's full `dotnet test Jusoor.sln` run and the deployed Supabase bucket/CORS setup. A successful compile/lint/typecheck does not verify external Storage behavior.
