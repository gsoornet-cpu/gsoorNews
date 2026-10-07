# Slice 26 — Client trial: CMS-selected video publishing

This is a partial source update for the existing Jusoor project. Extract it into the existing project root and allow the listed source files to overwrite. It does not include `.env`, dependency folders, build outputs, or uploaded media. Keep your local secrets and existing files intact.

## What this slice enables

- Editors can embed uploaded video from the existing CMS media library and choose one embedded video from the article for the public video hub.
- A selected hub video must be a ready Video asset embedded in that article. Draft, pending, failed, image, and unselected media are not exposed in `/videos`.
- The public API serves only Published articles with an explicitly selected Ready Video asset.
- `/videos` includes the latest featured native player, a paginated video-player grid, credits/dates, links to each article, and empty/error states.
- Existing article body videos continue to use the article page's media renderer and the current review/approval/publish workflow.

## Trial setup and safe test procedure

1. Use a separate trial/test PostgreSQL database and a dedicated test Supabase Storage bucket. Do not apply trial migrations to production.
2. Configure the API's server-side `SUPABASE_URL`, `SUPABASE_STORAGE_BUCKET`, and `SUPABASE_SERVICE_ROLE_KEY`; the service-role key must remain server-only. Configure the test bucket's public read policy and CORS for the trial web origin. Never paste secret values into this document or the archive.
3. From `backend`, with the trial database configuration active, apply pending migrations in order. If Slice 25 has not yet been applied, this command applies it before Slice 26:

   ```powershell
   dotnet ef database update --project src/Jusoor.Infrastructure/Jusoor.Infrastructure.csproj --startup-project src/Jusoor.Api/Jusoor.Api.csproj --context ApplicationDbContext
   ```

   Slice 26 adds `AddEditorialArticleFeaturedVideo`; it is a nullable relation, so existing stories remain unchanged. Confirm the target connection is the isolated trial database before running the command.

4. Log in to CMS and create a draft with a title, article body, and any needed presentation desks. In the rich-text editor, upload an MP4 or WebM video and insert it into the article body.
5. In “عرض فيديو من هذا الخبر في قسم الفيديوهات”, select the embedded video, save, then complete the existing newsroom review/approval/publish steps.
6. Verify the video does not appear at `/videos` while the article is a draft or in review. After publication, verify the featured video plays, its linked article opens, its credit/date render, and the article's embedded video plays on the article page.
7. Verify an article without a selected video remains absent; test more than 18 published video-bearing articles for pagination; test desktop/mobile and supported browsers. Also confirm that an archived or retracted article is removed from the public video feed through the existing workflow.

## Verification and limits

- Locally verified: backend solution build (0 warnings/errors), Application unit suite (753/753), EF reports no pending model changes, frontend full ESLint, TypeScript, and Next.js production build (including `/videos`).
- Not verified locally: Docker/Testcontainers integration, PostgreSQL application of the migration, real Supabase upload/CORS, and browser/client visual or playback acceptance. Those require the isolated trial environment and client run.
- One video per article may be selected for hub placement; the body may contain multiple inline media items. The hub uses native browser controls and the stored video file. Preview thumbnails, video category tabs, modal playback, and further page-parity work remain later client-trial slices.
- This trial workstream does not mark Phase 3 complete or start official Phase 4. Slice 25 client verification remains open; continue the roadmap sequence after client feedback.
