# Client trial implementation slices

**Purpose:** temporarily pull forward the already-planned newsroom and public-site work needed for a realistic client trial. This does not mark Phase 3 complete, change the roadmap order of completed work, or start the official Phase 4 (Automation + Geo-Intelligence).

The trial work follows the approved Final Demo and uses real published CMS content. Work is delivered one independently testable slice at a time; the client tests each handoff before the next slice starts.

## Slice 25 — Editorial desk assignment and public section pages

**Status:** implementation complete; client verification and isolated database migration remain.

The approved topical taxonomy (ten newsroom-managed categories) remains separate from Final Demo presentation desks. An article can be assigned to more than one desk. The v1 desk catalog covers the demo's 12 section slugs plus `opportunities` because it is also a live demo navigation destination:

`mughtarib`, `success`, `egypt`, `opportunities`, `official`, `events`, `red`, `lamma`, `secondgen`, `sports`, `arts`, `articles`, `various`.

**Delivered:** persisted `EditorialArticle.PresentationDesks`; safe allow-listed catalog validation; CMS multi-select before publication; API/public DTO support; Published-only desk feed filtering; `/category?cat=<slug>` with demo header, count, cards, empty state, canonical URL and pagination; backward-compatible migration defaults for existing articles; domain/application regression tests.

**Acceptance path:** create a CMS draft → select two different demo desks → save → publish through the existing newsroom workflow → confirm the article appears in both selected desk pages and no other desk; confirm the same article is absent before publication and after archive/retraction; verify page 2 for a desk exceeding 18 published items. Confirm the topical SEO category remains independent.

**Not included:** homepage's full demo module composition, section-specific editorial treatments, author/article visual parity, videos hub/uploading, and remaining demo routes. Those are later slices. Supabase media configuration is absent in the supplied local environment, so image/video upload is not a claim of this handoff.

## Planned client-trial sequence

| Order | Scope | Depends on |
|---|---|---|
| 25 | CMS desk assignment + public section listing (current handoff) | Phase 3 CMS / Q10 |
| 26 | Video publication and `/videos` discovery/player, using existing media library | 25 and verified Supabase bucket/CORS settings |
| 27 | News detail, images, author and related-content parity with the demo | 22, 23, 25 |
| 28 | Homepage demo sections populated from real CMS data; retain deliberate module-specific behavior | 25–27 and product mapping for section-specific types |
| 29 | Remaining demo routes, navigation/search/geography and responsive/accessibility parity | 25–28 and the corresponding real backend capabilities |
| 30 | Client-trial hardening and end-to-end verification in isolated staging | 25–29, Docker/PostgreSQL, Supabase and CSP/security work |

This list adds a client-trial workstream; it does not relabel the official Phase 4 work or mark any earlier slice as complete. Slice H (CSP and headers) remains a prerequisite to public staging/launch; an isolated client trial must not be exposed as public production.

## Slice 26 — CMS-selected video publishing and video hub

**Status:** implementation complete; client media upload, isolated database migration and browser acceptance remain.

**Delivered:** the CMS lets an editor choose one ready video already embedded in an article for hub placement. The selection is validated against the sanitized article body and the Ready Video media record; image, pending and failed media cannot be promoted as videos. The API returns only explicitly selected Ready Video assets attached to Published articles. `/videos` now has a featured native player, a paginated player grid, publication/credit metadata, links to the associated article, and honest loading-failure/empty states. Existing article video embeds remain available on the article page. Existing workflow authorization and publishing are unchanged.

**Migration:** `AddEditorialArticleFeaturedVideo` adds a nullable FK and index. Existing articles remain unfeatured. Apply it only to the isolated trial/test PostgreSQL database, never production as part of this handoff.

**Acceptance path:** configure the API with a dedicated test Supabase bucket and correct public read/CORS policy; migrate the isolated trial DB; sign in to CMS; create a draft, upload an MP4/WebM through the existing media library, insert it in the body, choose it for the video hub, save, and complete the current review/approval/publish workflow. Confirm it stays absent from `/videos` before publication; after publication, confirm its native player plays and its linked article opens. Confirm unselected embeds and non-ready media do not appear. Check pagination after 18 video-bearing articles, desktop/mobile layouts, and media behavior in supported browsers. Confirm an ordinary article with no selected video still works.

**Limits for this slice:** one CMS-selected hub video per article; an article may still contain multiple inline media items. The hub uses browser-native controls and the original video file; preview thumbnails, category-specific video tabs, modal playback and full demo detail parity are outside Slice 26. Supabase upload and real-browser verification were not available locally and remain client/developer acceptance items.

## Supplemental CMS delivery — crisis controls, Atmaen access, diaspora map and editorial monitoring

**Status:** code and local build checks complete; the user confirmed applying `20261020060238_AddCrisisModeConfiguration`. The SQL for `20261020063652_AddDiasporaEstimates` is prepared at `docs/20261020063652_AddDiasporaEstimates.sql` and still needs to be applied to the isolated development/trial database. On 2026-10-22, the execution environment could not reach Docker Desktop's Linux engine or PostgreSQL at `localhost:5433`; no database migration was applied. Live browser acceptance remains pending.

**Delivered in source:** CMS-managed crisis state and affected-country list with public crisis page/status; public Atmaen request flow and role-assigned CMS case queue; geography catalog management; a diaspora map fed only by curated estimates with source URL/date and explicit estimate labeling; a role-gated editorial monitoring report with 7/30/90-day totals, current workflow counts and the latest 30 transitions. Public community counts are suppressed below the approved `k=25` threshold, and country totals are suppressed when city cells are shown to avoid subtraction-based disclosure. Monitoring output intentionally omits actor IDs and internal transition reasons.

**Trial follow-up:** apply the diaspora migration using the isolated database only, rebuild/restart the development stack, seed/enter only sourced map estimates, and confirm the CMS routes through the actual role accounts. Atmaen local OTP is a development-only path; Railway preview must keep OTP fail-closed until Twilio is configured. Production Azure Key Vault and Twilio setup remain deferred by the user.

## Verification and handoff notes

- Do not apply the new migration to production as part of client trial. Apply it to an isolated trial/test PostgreSQL database first.
- The full API/Testcontainers and PostgreSQL migration behavior were not runnable in this environment because Docker is unavailable. The client/developer run is still required.
- Browser screenshot/visual comparison is pending; local demo file navigation was blocked by browser security policy. Static source and existing demo styles were inspected.
- The supplied project ZIP includes a populated `.env`; never repackage or send it. Build a clean upload archive from tracked/project source only and keep all runtime secrets out.
