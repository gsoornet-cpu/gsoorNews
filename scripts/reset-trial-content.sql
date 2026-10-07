-- =====================================================================
-- Jusoor — reset ALL editorial test content before handing over to the client.
-- Run against the TRIAL database only (Railway Postgres or your trial DB).
-- Removes: articles, their revisions, workflow history, corrections, media records.
-- Keeps:   users, roles, categories/tags, countries/cities, Atmaen cases, settings.
-- =====================================================================

-- STEP 0 (optional, run first and save the result): list the uploaded files so you
-- can also delete them from the Supabase Storage bucket afterwards.
-- SELECT "ObjectPath" FROM "EditorialMediaAssets";

BEGIN;

SELECT 'before' AS stage,
       (SELECT count(*) FROM "EditorialArticles")           AS articles,
       (SELECT count(*) FROM "EditorialArticleRevisions")   AS revisions,
       (SELECT count(*) FROM "EditorialArticleTransitions") AS transitions,
       (SELECT count(*) FROM "EditorialCorrections")        AS corrections,
       (SELECT count(*) FROM "EditorialMediaAssets")        AS media;

-- Children first, then articles, then media (articles reference media, not the reverse).
DELETE FROM "EditorialCorrections";
DELETE FROM "EditorialArticleTransitions";
DELETE FROM "EditorialArticleRevisions";
DELETE FROM "EditorialArticles";
DELETE FROM "EditorialMediaAssets";

SELECT 'after' AS stage,
       (SELECT count(*) FROM "EditorialArticles")           AS articles,
       (SELECT count(*) FROM "EditorialArticleRevisions")   AS revisions,
       (SELECT count(*) FROM "EditorialArticleTransitions") AS transitions,
       (SELECT count(*) FROM "EditorialCorrections")        AS corrections,
       (SELECT count(*) FROM "EditorialMediaAssets")        AS media;

-- All "after" numbers must be 0. If anything looks wrong, run ROLLBACK; instead.
COMMIT;
