# جسور (Jusoor) — backend foundation

Read `docs/ENGINEERING_ASSESSMENT.md` first — it explains what this is and why. This README is the practical setup path.

## Verification status

- **Phase 0** (auth, Identity, JWT, Docker Compose, CI foundation): confirmed working end-to-end on a real machine — Docker Compose, backend restore/build/test, EF migrations, frontend lint/build, and the core auth flow all verified.
- **Phase 1, Slice 1** (`Story`/`Article`/`RelevanceResult` domain + persistence): confirmed fully verified — build, 19 Domain + 6 Application unit tests, EF migration, integration tests all passing.
- **Phase 1, Slice 2** (SSRF-safe fetcher, RSS ingestion, `SourceFetchLog`): confirmed fully verified — build, 62 tests across 4 test projects, `InitialCreate` migration applied against real Postgres via Testcontainers, real register→login→JWT flow confirmed in logs.
- **Phase 1, Slice 3** (Hangfire scheduler): **implemented, not yet verified** — same sandbox limitation as always, no dotnet SDK here. Please build/test before relying on it.

## Known limitation: Hangfire Dashboard has no real access control yet

`/hangfire` currently falls back to Hangfire's default local-requests-only authorization — safe for local dev, **not** an access control suitable for any deployed environment. Our API is JWT-bearer only, and a browser navigating to a dashboard URL has no way to attach an Authorization header to that page load, so "just add `[Authorize]`" doesn't actually solve this the way it does for our other endpoints. A real fix (separate cookie-based admin login, or restricting the route at the reverse-proxy/network level) is planned, not built in this slice — flagging honestly rather than shipping something that looks secured but isn't.

## Known environment gotcha: port 5432 collisions

If you have a **native PostgreSQL install on Windows** (common — e.g. the EDB installer, which puts `PostgreSQL\<version>\bin` on `PATH`), it very likely already owns host port 5432. Docker Desktop's WSL2 port-forwarding can get silently shadowed by a natively-listening Windows service: `docker compose up` reports success, but connections from the host land on the native instance instead of the container, with a confusing "password authentication failed" error (Postgres deliberately gives that same message for a nonexistent role as for a wrong password).

Because of this, **the Postgres container's host-side port is 5433, not 5432** (see `docker-compose.yml`). Inside the Docker network, other containers still reach it as `postgres:5432` — only the host-facing port changed.

## Local setup

```bash
cp .env.example .env
# edit .env: set JWT_SIGNING_KEY (openssl rand -base64 64)

docker compose up -d postgres
# wait for the healthcheck, then from backend/:
cd backend
dotnet ef database update --project src/Jusoor.Infrastructure --startup-project src/Jusoor.Api
cd ..

docker compose up --build
```

- API: http://localhost:5000 (Swagger at `/swagger` in Development)
- Web: http://localhost:3000

## What's deliberately not here yet

Per the engineering plan's phasing, this repo currently covers: Phase 0 foundation + Phase 1 Slice 1 (news pipeline persistence) + Phase 1 Slice 2 (SSRF-safe fetcher, RSS source-worker strategy, manual ingestion trigger endpoint) + Phase 1 Slice 3 (Hangfire scheduler — recurring sweep enqueues one ingestion job per active Source every `Ingestion:ScheduleIntervalMinutes` minutes). Not yet built: additional source-format strategies (Atom/JSON), the Validate/Sanitize/cross-source-Dedup/Relevance/Classification pipeline stages, relevance engine implementation, AI gateway, pgvector/embeddings, Atmaen, crisis mode, CMS RBAC enforcement, and a real access-control solution for the Hangfire Dashboard (see limitation above).
