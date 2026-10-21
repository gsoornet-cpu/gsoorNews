# Jusoor project audit and execution plan

**Audit date:** 2026-10-19  
**Repository:** `D:\work\JusoorProject`  
**Audit stage:** source and roadmap review; runtime integrations and live deployment are not yet independently verified.

## 1. Product and architecture map

| Area | Current implementation | Main locations |
|---|---|---|
| Public website | Arabic-first Next.js 15 App Router; homepage, article detail, category feed, videos, search, sitemaps, responsive styles | `frontend/src/app`, `frontend/src/components`, `frontend/src/styles` |
| API | ASP.NET Core 10; controllers delegate to MediatR application commands/queries; JWT authentication and named role policies | `backend/src/Jusoor.Api`, `backend/src/Jusoor.Application` |
| Domain and persistence | Domain entities/enums; EF Core/PostgreSQL configurations and ordered migrations | `backend/src/Jusoor.Domain`, `backend/src/Jusoor.Infrastructure/Persistence` |
| CMS | Article list/editing, editorial workflow, SEO, media library, video selection, ad placement manager, taxonomy/desks | `frontend/src/app/cms`, `frontend/src/app/cms-api`, API editorial controllers |
| Media and advertising | Signed Supabase uploads and media validation; CMS ad assignments to allow-listed slot keys for web/mobile; public placement endpoint; author image field | `EditorialMedia*`, `EditorialAdPlacement*`, `AdvertisementsController`, `cms/advertisements` |
| Ingestion and review | RSS ingestion, SSRF defenses, source scheduling, relevance/clustering, human review queue | `backend/src/Jusoor.Application/Ingestion`, `Review`, `Relevance`; API controllers |
| Atmaen / geography | Profile, place, geography and case-related domain/API slices exist; several high-risk operational/business decisions and workflows remain deferred or unverified | `Profile`, `Atmaen`, `Geography`, `docs/decisions/PHASE2_DECISION_READINESS.md` |
| Operations | Docker Compose, deployment guidance, environment templates, CI and engineering progress ledger | `docker-compose.yml`, `.github`, `DEPLOY_RAILWAY_AR.md`, `docs/project-progress.html` |

## 2. Implementation status by product area

Statuses below combine the source code, migrations/tests present in the repository, and the progress ledger. A present migration or UI is evidence of implementation, not proof that it has been applied or works against production services.

| Area | Classification | Evidence / remaining work |
|---|---|---|
| Authentication and base API | Implemented; operational confirmation required | Identity/JWT and auth integration tests exist. Confirm current build/test pipeline and production bootstrap/role seeding. |
| Ingestion/relevance/review | Implemented for approved source layers | RSS, SSRF-safe fetching, clustering, relevance and human review are present. Additional source layers were explicitly deferred; do not invent credentials or scrape targets. |
| Editorial workflow and permissions | Implemented, with verification and security-hardening gaps | Workflow, transition audits, revision/correction, scheduling and role policies exist. Re-run complete suite; verify API authorization boundaries and production role bootstrap. |
| Article CMS and SEO | Implemented; manual acceptance remains | Rich text sanitizer, media references, SEO metadata, taxonomy and desk feeds exist. Article detail now includes related coverage from the same desk (and safely falls back to latest news); CMS browser workflow and deployment checks remain. |
| Ads and author images | Implemented in code, deployment/acceptance pending | Migration `20261016033050_AddEditorialAdPlacementsAndAuthorImage`; named web/mobile slot catalog; media upload/selection, target URL and per-surface save. CMS now validates every placement URL before its all-surfaces save; image search filters the entire server-side media library. Verify migration on isolated DB, every slot render, author image UI/public rendering, and mobile/desktop behavior. |
| Video publishing | Implemented; external service and browser acceptance pending | CMS selects an embedded Ready Video; public `/videos` uses published-only query. Supabase bucket/CORS/upload and browser playback remain acceptance dependencies. |
| Public site parity | Partially implemented | Home/article/category/video/search routes exist. Demo module composition and several demo routes are explicitly still planned in client-trial Slices 27–29. |
| Security hardening | Partially implemented; release gates remain | Production report-only CSP and baseline headers exist. Non-production production configs fail closed. An explicit `ClientDemoMode` now allows only `Staging` with stable local field encryption and OTP disabled; it requires an isolated trial database. Hangfire dashboard access, rate limiting, token staleness, role-management and production bootstrap gaps remain. |
| Atmaen Phase 2 remainder | Partially implemented / blocked on recorded decisions | Do not invent retention periods, k-anonymity thresholds, demographic sources, anonymous-submission policy, or response-network rules. Follow `docs/decisions/PHASE2_DECISION_READINESS.md`. |
| Frontend automated testing | Missing | No component or Playwright end-to-end suite is listed in the ledger; newsroom publish flow and responsive behavior need automated regression coverage. |
| Phase 4/5 | Not started | Do not mark or imply completion. |

## 3. Documentation and source-of-truth findings

1. `docs/project-progress.html` identifies itself as the live implementation ledger and records Slice 26 as the active client-trial handoff. Use it for current claimed status, then verify claims against source and actual runs.
2. `docs/CLIENT_TRIAL_SLICES.md` defines the client-trial dependency order: 25 desks, 26 video, 27 article/media parity, 28 real-data homepage composition, 29 remaining routes, 30 isolated trial hardening.
3. `docs/PHASE3_EDITORIAL_DECISIONS.md` is authoritative for approved newsroom decisions; `docs/decisions/PHASE2_DECISION_READINESS.md` preserves unresolved high-risk Atmaen decisions.
4. `docs/SLICE_20B_IMPLEMENTATION_PLAN.md` is stale: it says “plan only,” while the live ledger says implementation and backend/frontend verification are complete. Update this contradiction in a documentation-only stage; do not infer that the database migration is deployed.
5. `docs/project-progress.html` itself records older failing/pending results (e.g. Slice 20c test rerun) and pending integrations. Fresh verification is required before changing any item to verified.

## 4. Prioritized execution checklist

### Stage A — establish a truthful baseline and clear release blockers

- [x] Run frontend type check and source lint. **TypeScript:** `node node_modules/typescript/bin/tsc --noEmit --incremental false` exited 0. **ESLint:** `npm run lint` exits 0 with no warnings after stabilizing the VideoModal mount-time embed value.
- [ ] Verify the frontend standalone production artifact. TypeScript and ESLint pass; `next build` compiled successfully, passed Next's lint/type phase, and generated all 18 static pages. Final standalone trace copying still fails on Windows with `EPERM` creating nested paths under `frontend/.next/standalone/node_modules/next`; container artifact verification remains open.
- [x] Run the backend solution build. **Build:** `dotnet build Jusoor.sln --no-restore --disable-build-servers -m:1 /p:UseSharedCompilation=false` succeeded (0 warnings/errors) after repairing test-only compile breaks caused by the new ads interface and role-aware list query.
- [ ] Run backend tests. The Infrastructure unit-test project compiled, including the new disabled-demo-OTP tests, but VSTest again could not connect to its testhost after 90 seconds; no test pass is claimed. Docker Desktop is running per user, but this execution environment gets `permission denied` connecting to `dockerDesktopLinuxEngine`; API/Testcontainers verification is therefore still blocked.
- [x] Check EF model snapshot consistency. `dotnet ef migrations has-pending-model-changes --project src/Jusoor.Infrastructure --startup-project src/Jusoor.Api --no-build` reported no pending model changes.
- [ ] Check Docker/PostgreSQL access, Supabase configuration presence (never print secrets), and migration status on an isolated database. Docker was started but its engine pipe remains inaccessible from this execution sandbox; no DB migration was applied.
- [x] Reconcile Slice 20b documentation status; update the live progress ledger only from verified evidence. Corrected the live entry's broken link and removed a stale “plan only” claim; detailed plan already records implementation verification and the remaining manual/DB checks.
- [x] Implement the mobile header logo sizing and improve footer logo rendering. **Header implementation:** 80px portrait / 64px landscape with natural aspect ratio and responsive width. **Footer:** reuse the project’s transparent PNG matching the supplied original logo; remove the CSS filter that was turning the brand mark white and enlarge it to 72px while preserving its aspect ratio. Browser viewport verification remains pending.

**Frontend checks so far:** see exact TypeScript, ESLint, and production-build outcomes above. No browser viewport or real-device verification was available in this stage.

### Stage B — security and operational readiness (highest launch priority)

- [ ] Validate CSP end-to-end and narrow the policy from production browser reports before enforcement. Production-only `Content-Security-Policy-Report-Only` plus baseline headers are in `frontend/next.config.js`; policy/header assertions pass. Browser delivery/violation review remains open. No report collector or enforcement is claimed.
- [x] Prevent unauthenticated Hangfire dashboard exposure: `/hangfire` is now mapped only in Development and can be disabled with `Hangfire:DashboardEnabled=false`. Staging/Production continue running background jobs but do not expose the dashboard. A browser-compatible admin dashboard remains a future feature if operations require it.
- [ ] Confirm production role seeding, role-revocation/token freshness, atomic last-admin behavior and audit requirements. Security config now includes an isolated `ClientDemoMode`: `Staging` only, requires a stable local encryption key, and routes OTP to a provider that fails closed without logging codes. The production path still requires Azure Key Vault + Twilio. This is not a production substitute; Railway/database startup and isolated preview acceptance remain pending.
- [ ] Review upload validation, authorization, SSRF protections, secret handling, rate limits, error exposure, and dependency advisories.
- [ ] Verify every migration and scheduled-publishing job on disposable PostgreSQL; never apply trial migrations to production during this work.

### Stage C — CMS and public-site client-trial completion

- [ ] Accept desks and video workflow against isolated PostgreSQL and a dedicated Supabase bucket, including browser/mobile playback.
- [x] Implement article detail, author-image, related-content, article sharing, author archive and country-filtered published news (Slice 27). Real-browser and trial-content acceptance remain.
- [x] Preserve the CMS-backed homepage modules, section feeds, featured videos and surface-specific ad placements (Slice 28 source implementation). Editorial mapping and browser acceptance with representative trial content remain.
- [x] Remove homepage UI that implied unavailable functionality: deleted the non-persistent poll with fabricated 72%/28% results and replaced the inactive newsletter form with links to the real Crisis Center and Atmaen flow. TypeScript and ESLint pass after this change.
- [x] Implement remaining public about/privacy/account/country routes alongside existing navigation, search, geography, crisis and service routes (Slice 29). Account recovery/phone verification and final legal review are explicitly not implemented; responsive/accessibility browser acceptance remains.
- [ ] Add CMS e2e coverage for article draft → review → approval → publish, scheduling, media/video, SEO and ad placement authorization.
- [ ] Verify ad CRUD end to end for each allow-listed slot and each supported surface, including creative upload, target-link validation, preview, and public render. Fixed the CMS save validation to validate both surfaces, because the API persists the complete web/mobile assignment set in one request.
- [ ] Complete isolated client-trial hardening (Slice 30); keep staging private until Stage B's CSP/security gates pass.
- [ ] Implement the separately requested CMS-controlled homepage section manager (create/enable/layout/desk/order for web and mobile) and navigation order/visibility manager. The current presentation desk catalog is not a CMS-managed layout or navigation configuration.
- [ ] Implement tailored sports content modeling and CMS fields for Egyptian professional players abroad, naturalized Egyptian players and the Egyptian league. The current `sports` desk only tags and feeds articles.

### Stage D — Phase 2 and later roadmap, only where decisions unblock it

- [ ] Continue Atmaen in the sequence and boundaries approved by `PHASE2_DECISION_READINESS.md`; retain explicit legal/data dependencies.
- [ ] Keep deferred ingestion layers and Phase 4/5 out of scope until their documented decisions, data, credentials, and dependencies are ready.

## 5. Acceptance and reporting rules

- A feature is not “verified” from source presence alone. Record the exact command, environment and result.
- Database migration application, Supabase upload/CORS, browser acceptance, and production deployment are separate claims.
- Keep migrations backward-compatible and use isolated disposable databases for trial acceptance.
- Preserve existing local changes. At audit start, Git reported a pre-existing deletion of tracked `17-10.zip`; it is left untouched.
- The first code implementation stage in this work is only the mobile header logo sizing from the user's request. Footer logo extraction/redesign waits for the original source asset.
