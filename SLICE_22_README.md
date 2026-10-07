# Phase 3 — Slice 22 patched handoff

Overlay these complete files onto the existing `D:\work\JusoorProject` tree. This package excludes `.env`, credentials, build artifacts, and `node_modules`.

The sanitizer now filters attributes per element: article paragraphs do not accept `title`; iframes receive only the fixed canonical provider attributes; images/video require a media asset ID and the server requires that asset to be ready.

Before use, configure the API-only Supabase variables, create the public-read bucket with the file limits/types and CMS CORS described in `docs/PHASE3_EDITORIAL_DECISIONS.md` §5.5, then run the `AddEditorialMediaAssets` EF migration. Public bucket objects are readable immediately after upload; upload only media cleared for public release.

Run `dotnet test Jusoor.sln` from `backend`, and `npm run lint` plus `npm run build` from `frontend`. The last developer run was 1016/1017; rerun after this fix and send any remaining failure output.