import type { MetadataRoute } from "next";
import { absoluteUrl } from "@/lib/site";

// Read SITE_URL at request time, not at build time.
export const dynamic = "force-dynamic";

/** /robots.txt — keeps crawlers out of the newsroom and API proxies; advertises the sitemaps (Slice 18). */
export default function robots(): MetadataRoute.Robots {
  const sitemaps = [absoluteUrl("/sitemap.xml"), absoluteUrl("/news-sitemap.xml")].filter(
    (url): url is string => url !== null,
  );

  return {
    rules: { userAgent: "*", allow: "/", disallow: ["/cms", "/cms-api", "/api/"] },
    ...(sitemaps.length > 0 ? { sitemap: sitemaps } : {}),
  };
}
