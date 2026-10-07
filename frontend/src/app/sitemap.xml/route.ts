import { getPublishedNewsSitemap } from "@/lib/news-api";
import { articleUrl, buildSitemapXml, toIsoDate, type SitemapUrl } from "@/lib/seo";
import { absoluteUrl, getSiteUrl } from "@/lib/site";

// Always fresh: an unpublished article must leave the sitemap immediately.
export const dynamic = "force-dynamic";

/** /sitemap.xml — the homepage plus every published article (Slice 18, spec §16). */
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

  const urls: SitemapUrl[] = [{ loc: absoluteUrl("/")!, lastmod: toIsoDate(entries[0]?.updatedAtUtc) }];
  for (const entry of entries) {
    urls.push({ loc: absoluteUrl(articleUrl(entry))!, lastmod: toIsoDate(entry.updatedAtUtc) });
  }

  return new Response(buildSitemapXml(urls), {
    headers: { "Content-Type": "application/xml; charset=utf-8", "Cache-Control": "no-cache" },
  });
}
