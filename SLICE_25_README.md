# Slice 25 — Client trial: news desk assignment

This is a partial source update for the existing Jusoor project. Extract it into the existing project root and allow the listed source files to overwrite. Do not replace or share the local `.env` file; it is intentionally excluded from this archive.

## What this slice enables

- CMS articles can be assigned to multiple Final Demo presentation desks before publishing.
- Presentation desks are separate from SEO topic categories and tags.
- `/category?cat=<desk-slug>` shows only published articles assigned to that desk, with the Final Demo category heading, count, empty state and pagination.
- Existing articles receive an empty desk list through the database migration.

The available desk slugs are `mughtarib`, `success`, `egypt`, `opportunities`, `official`, `events`, `red`, `lamma`, `secondgen`, `sports`, `arts`, `articles`, and `various`.

## Safe test procedure

1. Use a separate trial/test PostgreSQL database and storage bucket/configuration. Do not apply this migration to production.
2. From `backend`, apply the EF migration with your trial database configuration:

   ```powershell
   dotnet ef database update --project src/Jusoor.Infrastructure/Jusoor.Infrastructure.csproj --startup-project src/Jusoor.Api/Jusoor.Api.csproj --context ApplicationDbContext
   ```

3. Log into the CMS, create a draft, choose two desks, save, then complete the newsroom review and publish workflow.
4. Confirm the published story appears in both desk pages; confirm draft/unpublished stories do not appear. Test an empty desk and pagination after more than 18 stories are published to one desk.
5. Verify the desk URLs and layouts in desktop and mobile browsers. Then send the findings before starting Slice 26.

Video upload/discovery is not part of this slice. The supplied local environment did not have Supabase media settings, and video requires separate configuration and verification.
