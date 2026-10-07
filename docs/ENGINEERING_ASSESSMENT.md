# Slice 20b — Retracted / Archived states: implementation plan

**Status:** plan only, nothing implemented. **Contract:** `PHASE3_EDITORIAL_DECISIONS.md` §5.2 (Q9). **Depends on:** Slices 19, 20, 21 (all verified at build/test level).
**Why it is its own slice:** it changes the single public visibility rule (`Status == Published`) and two sitemaps, which is the riskiest kind of change in a newsroom CMS: a mistake either leaks retracted text or hides live journalism.

## 1. Scope

In: two new states and four new transitions, public handling of both, sitemap changes, CMS buttons, migration, tests.
Out (other slices): Q1/Q2/Q3/Q12/Q13/Q14 behaviour changes (Slice 20c), slugs (23), scheduling (24), CSP (H).

## 2. Domain

- `EditorialArticleStatus`: add `Retracted = 6`, `Archived = 7`. Update the enum comment (5 stays reserved for Scheduled).
- `EditorialArticle` new columns/properties: `RetractedAtUtc`, `RetractedByUserId`, `RetractionNotice` (≤1000, the public text), `ArchivedAtUtc`, `ArchivedByUserId`.
- New methods, each throwing `InvalidOperationException` on a wrong source state (same style as `Unpublish`):
  - `Retract(userId, notice, now)` — only from `Published`. **Keeps** `PublishedAtUtc` (the original publication date is part of the public record).
  - `Archive(userId, now)` — only from `Published`.
  - `RestoreFromArchive()` — only from `Archived`; clears the archive fields.
  - `ReinstateRetracted()` — only from `Retracted`; goes to `Draft`, clears retraction and publication fields (a later publish records a fresh time, like `Unpublish`).
- The internal reason is not a column: it goes in the transition row (`reason`), which already exists and is append-only.

## 3. Application

- `EditorialAction`: add `Retract`, `Archive`, `RestoreArchived`, `ReinstateRetracted`.
- `EditorialArticleAccess.CheckTransition` (single home of the rules):

| Action | From | Roles |
|---|---|---|
| Retract | Published | Senior, Managing, EIC |
| Archive | Published | Senior, Managing, EIC |
| RestoreArchived | Archived | Senior, Managing, EIC |
| ReinstateRetracted | Retracted | EIC only |

  SystemAdmin gets none of the four (the audited break-glass unpublish remains the emergency tool). Role is checked before state, as everywhere.
- Edit lock: the edit rule "Senior/Managing/EIC edit any article in any state" gets an exception — `Retracted` and `Archived` are not editable (`InvalidState`). `CheckCorrection` accepts `Published` **and** `Archived`, not `Retracted`.
- Four commands following the existing pattern in `EditorialWorkflowCommands.cs` / `EditorialWorkflow.ApplyAsync` (one `SaveChanges` writes the article and its transition row; denied or conflicting attempts write nothing):
  - `RetractEditorialArticleCommand(articleId, publicNotice, internalReason)` — both mandatory; notice 10–1000, reason ≤1000 (FluentValidation, like the other commands).
  - `ArchiveEditorialArticleCommand(articleId, reason?)`, `RestoreEditorialArticleFromArchiveCommand(articleId, reason?)`.
  - `ReinstateRetractedEditorialArticleCommand(articleId, reason)` — reason mandatory.
- Public queries (`EditorialQueries.cs`):
  - `GetPublishedNewsQuery` — **unchanged** (Published only). This is what keeps both new states out of every feed.
  - `GetPublishedNewsByIdQuery` — returns a three-way result instead of `DTO?`: `Found(PublicNewsDetailDto with IsArchived, ArchivedAtUtc)`, `Retracted(PublicRetractionNoticeDto: Id, Title, PublishedAtUtc, RetractedAtUtc, Notice)`, or `NotFound`. The retracted branch must **project only those fields** — the body is never loaded into a DTO.
  - `GetPublishedNewsSitemapQuery` — returns Published + Archived rows with an `IsArchived` flag on `PublicNewsSitemapEntryDto`. Retracted is excluded.
- Check every other reader of `EditorialArticles` for the same assumption (CMS list status filter, history endpoints, SEO helpers) so none of them quietly treats "not Draft" as "public".

## 4. API

- `EditorialArticlesController`: `POST {id}/retract`, `{id}/archive`, `{id}/restore-archive`, `{id}/reinstate`, each with the same coarse role policy as the existing transition endpoints and the same 404 / 403 / 409 mapping.
- `NewsController` `GET {id}`: 200 (article), **410** with the notice JSON (retracted), 404. Add `Cache-Control: no-store` on the 410 so a CDN never pins a stale state in either direction.
- No change to `GET /news` (list).

## 5. Database

- Migration `AddEditorialRetractedArchived`: five nullable columns on `EditorialArticles`. The status column is already an int, so no data change.
- Check constraint: `Status <> 6 OR RetractionNotice IS NOT NULL` — a retracted row without a public notice must be impossible, even if code is wrong.
- Generate with `dotnet ef migrations add` on your machine (the authoring sandbox cannot restore NuGet, so any migration written here is hand-written and must be checked against the model snapshot).

## 6. Frontend

- Public article page: handle the three results.
  - Archived: normal article plus an "أرشيف" label and "نُشر في …" line; corrections box unchanged.
  - Retracted: notice page (headline, retraction date, the public notice, link home). `robots: noindex`, no Open Graph description from the article, no JSON-LD.
  - **Known constraint:** the Next.js App Router cannot set an HTTP status from a page component. The API returns a true 410, but the public page will return 200 + `noindex` unless we add a small middleware/route-handler spike. Recommendation: ship with `noindex` (search engines honour it on recrawl) and do the 410 spike only if removal is too slow. This is the one place the plan falls short of the Q9 wording; it is recorded rather than hidden.
- `sitemap.xml` includes Archived; `news-sitemap.xml` drops entries with `IsArchived`.
- CMS: status badges for the two new states; buttons shown only when the user's roles and the article's state allow them (the server remains the authority); Retract opens a modal with two fields (public notice, internal reason); history panel and transition list render the new status names.

## 7. Tests (written with the slice)

- **Domain:** each method's source-state guard; fields set and cleared; `Retract` keeps `PublishedAtUtc`; enum values pinned to 6 and 7 (persisted as int).
- **Access matrix:** every role × the four actions × every state; edit lock on Retracted/Archived; `CheckCorrection` on Archived (allowed) and Retracted (invalid).
- **Handlers:** success writes exactly one transition row; mandatory notice/reason; denied and conflicting attempts write nothing; not-visible = not-found.
- **Public queries (the important ones):** Retracted and Archived are absent from the list; detail returns 200 for Archived and the notice for Retracted; **the retracted result contains no body text** (assert on the DTO type and on a body marker string); main sitemap contains Archived and excludes Retracted; the news sitemap excludes both.
- **Integration (Testcontainers):** migration applies; check constraint rejects a retracted row without a notice; 410 over HTTP; `Cache-Control` header.
- **Frontend:** type-check, lint, build; render all three states against a stand-in API and confirm the retracted page never contains the body.

## 8. Manual verification checklist

Publish an article → archive it → confirm it is gone from home/category but readable by URL, present in `sitemap.xml`, absent from the news sitemap → restore it. Publish another → retract with a notice → confirm the URL shows only the notice, the API answers 410, it is absent from both sitemaps and every feed, the body does not appear in page source → as a non-EIC senior, confirm you cannot reinstate → as EIC, reinstate and confirm it lands in Draft. Check the transition history shows all of it with roles.

## 9. Risks and order of work

1. **Body leak on retraction** — mitigated by the DTO shape and its test; review every endpoint that returns article bodies.
2. **Hidden live article** — the main sitemap and detail query must include Archived; covered by explicit tests.
3. **Caching** — `no-store` on 410; purge social/CDN caches is an operational step, noted for launch.
4. Suggested order: domain → access matrix + tests → commands + tests → queries + tests → controller → migration → frontend → manual pass. Each step builds and tests green before the next.
