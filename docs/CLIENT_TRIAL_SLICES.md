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

## Slice 27 — article, author, sharing and related content

**Status:** source implementation complete; browser/content acceptance remains.

**Delivered:** article bylines link to a public author archive; the archive filters the Published-only news feed by author and includes the available author image. Article pages provide Web Share where supported, copy-link, print, and direct social-share links, plus published taxonomy links and more from the same author/desk. The API accepts an optional country id for Published-only archive filtering, used by Slice 29's country route. Existing article-body sanitization, media rendering, corrections and retraction behavior remain the source of truth.

**Limits:** the system does not record article view counts, so “most read” is not presented as fact. A separate image-caption field is not available in the article contract. Validate author images, sharing targets, print layout, related articles, archived/retracted content and mobile behavior against actual trial content.

## Slice 28 — homepage modules from published CMS content

**Status:** existing CMS-backed module implementation retained; final editorial mapping and browser acceptance remain. Removed the non-persistent homepage poll and hard-coded 72%/28% figures, which were demo-only and not backed by a voting service. The former newsletter CTA also did not save subscriptions; it now links to the real Crisis Center and Atmaen service rather than collecting an email without a working consent/subscription flow.

The homepage already composes its news sections from Published-only desk feeds and CMS-selected home videos, with desktop/mobile ad placements resolved from CMS assignments. Unsupported voting and email-subscription widgets are not presented as working features. Review every section against the approved Final Demo with representative trial data, especially ordering, empty states, ad density and mobile carousel behavior.

## Slice 29 — remaining public routes and account navigation

**Status:** source implementation complete for implemented API capabilities; legal, browser and data acceptance remain.

**Delivered:** `/country?code=<ISO-2>` resolves the live country catalog and displays only Published news assigned to that country; `/about` describes existing news and service capabilities without demo statistics; `/privacy` states the trial privacy behavior and makes the unresolved retention policy explicit; `/auth` provides real login/account registration via a separate HttpOnly user cookie, displays the authenticated profile, updates country/city privacy level, and adds/removes followed cities through authenticated profile APIs. Search, country catalog, crisis, Atmaen, diaspora map, category, article and video routes already exist.

**Limits:** account recovery, phone verification, OAuth, and region-level location selection are not exposed. Registration uses the backend's current email/password endpoint; it is not evidence of verified identity. The privacy page is a trial disclosure, not the final legally approved policy. Ensure country/city seed data exists and exercise navigation, keyboard focus, screen-reader labels, responsive layouts and account cookie behavior in a real browser before client acceptance.

## Slice 30 — isolated trial hardening

**Status:** still open; cannot be marked complete from source compilation.

Requires end-to-end browser and device acceptance, migration checks on the isolated database, seeded trial accounts and countries, dedicated Supabase media upload/CORS validation, security/CSP review, role-specific access review, and confirmation that staging remains private. The user confirmed the local EF command reports “No migrations were applied. The database is already up to date”; this is a local-database observation only and does not verify Railway or a separate trial database.

**Source checks on 2026-10-22:** frontend TypeScript (`node node_modules/typescript/bin/tsc --noEmit --incremental false`) and ESLint (`npm run lint -- --no-cache`) pass after the homepage CTA/poll cleanup. These checks do not close the browser, database, storage, security or deployment acceptance items above.

## Requested CMS capabilities tracked outside slices 25–30

The following requirements were explicitly requested during the CMS design discussion but are not delivered by the trial slices above. They must not be inferred from the existing article presentation-desk taxonomy:

- CMS-managed homepage collections: create/enable/disable a section, choose from approved homepage layouts, map it to an editorial desk, and set its order consistently on web and mobile.
- CMS-managed navigation order/visibility for desktop and mobile, with permission checks, safe route validation and public rendering from the saved configuration.
- Structured sports coverage tailored to Egyptian professionals abroad, naturalized Egyptian players and the Egyptian league. The current `sports` presentation desk is only an article label/feed; it is not a sports data model or tailored sports CMS.

These need their own implementation slices, persistence/API contracts and CMS acceptance. No UI-only control should be added until its saved configuration actually controls both the website and mobile layout.

## Slice 26 — CMS-selected video publishing and video hub

**Status:** implementation complete; client media upload, isolated database migration and browser acceptance remain.

**Delivered:** the CMS lets an editor choose one ready video already embedded in an article for hub placement. The selection is validated against the sanitized article body and the Ready Video media record; image, pending and failed media cannot be promoted as videos. The API returns only explicitly selected Ready Video assets attached to Published articles. `/videos` now has a featured native player, a paginated player grid, publication/credit metadata, links to the associated article, and honest loading-failure/empty states. Existing article video embeds remain available on the article page. Existing workflow authorization and publishing are unchanged.

**Migration:** `AddEditorialArticleFeaturedVideo` adds a nullable FK and index. Existing articles remain unfeatured. The user reports their local EF database is up to date; verify that the same isolated trial database is targeted before client acceptance. Never apply trial migrations to production as part of this handoff.

**Acceptance path:** configure the API with a dedicated test Supabase bucket and correct public read/CORS policy; migrate the isolated trial DB; sign in to CMS; create a draft, upload an MP4/WebM through the existing media library, insert it in the body, choose it for the video hub, save, and complete the current review/approval/publish workflow. Confirm it stays absent from `/videos` before publication; after publication, confirm its native player plays and its linked article opens. Confirm unselected embeds and non-ready media do not appear. Check pagination after 18 video-bearing articles, desktop/mobile layouts, and media behavior in supported browsers. Confirm an ordinary article with no selected video still works.

**Limits for this slice:** one CMS-selected hub video per article; an article may still contain multiple inline media items. The hub uses browser-native controls and the original video file; preview thumbnails, category-specific video tabs, modal playback and full demo detail parity are outside Slice 26. Supabase upload and real-browser verification were not available locally and remain client/developer acceptance items.

## Supplemental CMS delivery — crisis controls, Atmaen access, diaspora map and editorial monitoring

**Status:** code and local build checks complete; the user confirmed applying `20261020060238_AddCrisisModeConfiguration`. The user later ran `dotnet ef database update` successfully and reported “No migrations were applied. The database is already up to date.” This confirms only the database targeted by that local command; it does not independently identify or verify a separate isolated trial database. Live browser acceptance remains pending.

**Delivered in source:** CMS-managed crisis state and affected-country list with public crisis page/status; public Atmaen request flow and role-assigned CMS case queue; geography catalog management; a diaspora map fed only by curated estimates with source URL/date and explicit estimate labeling; a role-gated editorial monitoring report with 7/30/90-day totals, current workflow counts and the latest 30 transitions. Public community counts are suppressed below the approved `k=25` threshold, and country totals are suppressed when city cells are shown to avoid subtraction-based disclosure. Monitoring output intentionally omits actor IDs and internal transition reasons.

**Trial follow-up:** apply the diaspora migration using the isolated database only, rebuild/restart the development stack, seed/enter only sourced map estimates, and confirm the CMS routes through the actual role accounts. Atmaen local OTP is a development-only path; Railway preview must keep OTP fail-closed until Twilio is configured. Production Azure Key Vault and Twilio setup remain deferred by the user.

## Verification and handoff notes

- Do not apply the new migration to production as part of client trial. Apply it to an isolated trial/test PostgreSQL database first.
- The full API/Testcontainers and PostgreSQL migration behavior were not runnable in this environment because Docker is unavailable. The client/developer run is still required.
- Browser screenshot/visual comparison is pending; local demo file navigation was blocked by browser security policy. Static source and existing demo styles were inspected.
- The supplied project ZIP includes a populated `.env`; never repackage or send it. Build a clean upload archive from tracked/project source only and keep all runtime secrets out.
