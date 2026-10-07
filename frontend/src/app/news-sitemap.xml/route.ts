import { getPublishedNewsSitemap } from "@/lib/news-api";
import { articleUrl, buildNewsSitemapXml, selectNewsSitemapEntries, toIsoDate } from "@/lib/seo";
import { absoluteUrl, getSiteUrl } from "@/lib/site";

// Always fresh: the Google News sitemap must reflect publishes immediately.
export const dynamic = "force-dynamic";

/** /news-sitemap.xml — Google News sitemap: articles published in the last 48 hours, max 1000. */
export async function GET() {
  if (!getSiteUrl()) {
    return new Response("SITE_URL is not configured.", { status: 503 });
  }

  let entries;
  try {
    entries = await getPublishedNewsSitemap();
  } catch {
    return new Response("Sitemap temporarily unavailable.", { status: 503, headers: { "Retry-After": "60" } });
  }

  const items = selectNewsSitemapEntries(entries.filter((entry) => !entry.isArchived), new Date()).flatMap((entry) => {
    const publishedAt = toIsoDate(entry.publishedAtUtc);
    return publishedAt ? [{ loc: absoluteUrl(articleUrl(entry))!, title: entry.title, publishedAt }] : [];
  });

  return new Response(buildNewsSitemapXml(items), {
    headers: { "Content-Type": "application/xml; charset=utf-8", "Cache-Control": "no-cache" },
  });
}
