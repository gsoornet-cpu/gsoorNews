# Phase 3 — Slice 23 SEO handoff

This delivery adds the SEO metadata and taxonomy workflow to the newsroom CMS.

## Apply the database migrations

From `backend`:

```powershell
dotnet ef database update --project src/Jusoor.Infrastructure --startup-project src/Jusoor.Api
```

The migrations are `AddEditorialSeoMetadata` and `AddEditorialSeoTaxonomy`. Existing articles receive stable `article-{id}` slugs. The taxonomy migration seeds the ten primary categories and initializes the tag array for existing rows.

## Verify

From `backend`:

```powershell
dotnet test Jusoor.sln
```

From `frontend`:

```powershell
npm run lint
npm run build
```

API build, TypeScript, ESLint, and EF model/snapshot checks passed in the implementation environment. The complete test suite was not verified there: VSTest could not connect to the testhost during the focused domain test run. The frontend build compiled and generated static pages, but Windows returned `EPERM` while Next.js wrote standalone build traces. Run the commands above in the developer environment and send the result before Slice 24.

## CMS entry points and roles

- SEO article list: `/cms/seo`
- Per-article SEO editor: `/cms/seo/articles/{id}`
- `SeoEditor`, `SeniorEditor`, `ManagingEditor`, and `EditorInChief` can edit SEO metadata. SEO API responses do not include article bodies.
- Only `EditorInChief` can add or deactivate primary categories.
- Articles need an active primary category before their SEO metadata can be saved. Secondary tags are optional (up to 12, 40 characters each).

## Public SEO behavior

- Current article paths are `/article/{id}/{slug}`; old ID-only paths continue to resolve through permanent redirects. Next.js `permanentRedirect` emits HTTP 308.
- `noindex` articles are omitted from both sitemaps. Canonical, Open Graph, Twitter, and NewsArticle JSON-LD use the saved metadata with newsroom title/summary fallbacks.
- The demo's curated collections are not all topical categories. Their reconciliation is recorded in `PHASE3_EDITORIAL_DECISIONS.md`.
