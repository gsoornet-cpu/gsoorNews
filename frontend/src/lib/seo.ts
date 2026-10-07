// Pure SEO helpers (no I/O): descriptions, Schema.org JSON-LD and sitemap XML.
// Slice 18, spec §16. Kept free of framework imports so they can be exercised
// directly by a script.
import { htmlToPlainText } from "./html";
import type { NewsDetail, NewsSitemapEntry } from "./news-api";
import { EDITORIAL_BYLINE, LOGO_PATH, SITE_LANGUAGE, SITE_NAME, absoluteUrl } from "./site";

export type JsonLdObject = Record<string, unknown>;

/** Google News only wants articles from the last two days, at most 1000 per sitemap. */
export const NEWS_SITEMAP_WINDOW_MS = 48 * 60 * 60 * 1000;
export const NEWS_SITEMAP_MAX_URLS = 1000;

/** Collapses whitespace and cuts at a word boundary with an ellipsis. Returns "" for empty input. */
export function toPlainDescription(text: string | null | undefined, maxLength = 160): string {
  const flat = (text ?? "").replace(/\s+/g, " ").trim();
  if (flat.length <= maxLength) {
    return flat;
  }

  let cut = flat.slice(0, maxLength - 1);
  if (/[\uD800-\uDBFF]$/.test(cut)) {
    cut = cut.slice(0, -1); // never split a surrogate pair
  }

  const lastSpace = cut.lastIndexOf(" ");
  const base = lastSpace > maxLength * 0.6 ? cut.slice(0, lastSpace) : cut;
  return `${base.trimEnd()}…`;
}

export function toIsoDate(value: string | null | undefined): string | undefined {
  if (!value) {
    return undefined;
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? undefined : date.toISOString();
}

export function articleDescription(article: Pick<NewsDetail, "summary" | "body">): string {
  // The body is sanitized HTML (Slice 21): a description must be text, never markup.
  return toPlainDescription(article.summary ?? htmlToPlainText(article.body));
}

export function articleUrl(article: Pick<NewsDetail, "id" | "slug">): string {
  return `/article/${article.id}/${encodeURIComponent(article.slug)}`;
}

export function buildOrganizationJsonLd(): JsonLdObject | null {
  const url = absoluteUrl("/");
  const logo = absoluteUrl(LOGO_PATH);
  if (!url || !logo) {
    return null;
  }

  return { "@context": "https://schema.org", "@type": "Organization", name: SITE_NAME, url, logo };
}

export function buildArticleJsonLd(article: NewsDetail): JsonLdObject | null {
  const url = absoluteUrl(article.canonicalUrl || articleUrl(article));
  const home = absoluteUrl("/");
  const logo = absoluteUrl(LOGO_PATH);
  if (!url || !home || !logo) {
    return null;
  }

  const place = article.cityNameAr ?? article.countryNameAr ?? null;

  // undefined members are dropped by JSON.stringify, so absent data is omitted, never faked.
  return {
    "@context": "https://schema.org",
    "@type": "NewsArticle",
    mainEntityOfPage: { "@type": "WebPage", "@id": url },
    url,
    headline: article.seoTitle || article.title,
    articleSection: article.primaryCategoryNameAr || undefined,
    keywords: article.secondaryTags.length ? article.secondaryTags.join(", ") : undefined,
    description: article.seoDescription || articleDescription(article) || undefined,
    inLanguage: SITE_LANGUAGE,
    datePublished: toIsoDate(article.publishedAtUtc),
    dateModified: toIsoDate(article.updatedAtUtc),
    author: {
      "@type": article.authorName ? "Person" : "Organization",
      name: article.authorName || EDITORIAL_BYLINE,
      url: home,
    },
    publisher: { "@type": "Organization", name: SITE_NAME, url: home, logo: { "@type": "ImageObject", url: logo } },
    contentLocation: place ? { "@type": "Place", name: place } : undefined,
  };
}

export function buildBreadcrumbJsonLd(items: { name: string; path: string }[]): JsonLdObject | null {
  const elements = [];
  for (const [index, item] of items.entries()) {
    const url = absoluteUrl(item.path);
    if (!url) {
      return null;
    }
    elements.push({ "@type": "ListItem", position: index + 1, name: item.name, item: url });
  }

  return { "@context": "https://schema.org", "@type": "BreadcrumbList", itemListElement: elements };
}

/**
 * Serialises JSON-LD for inlining in a <script>. Article titles come from
 * editors, so "<" is escaped: a title containing "</script>" must not be able
 * to close the tag and inject markup.
 */
export function serializeJsonLd(data: unknown): string {
  return JSON.stringify(data)
    .replace(/</g, "\\u003c")
    .replace(/\u2028/g, "\\u2028")
    .replace(/\u2029/g, "\\u2029");
}

function isXmlChar(code: number): boolean {
  return (
    code === 0x9 ||
    code === 0xa ||
    code === 0xd ||
    (code >= 0x20 && code <= 0xd7ff) ||
    (code >= 0xe000 && code <= 0xfffd) ||
    (code >= 0x10000 && code <= 0x10ffff)
  );
}

/** Escapes text for XML and drops characters XML 1.0 forbids (one stray control character would make search engines reject the whole sitemap). */
export function escapeXml(value: string): string {
  let clean = "";
  for (const ch of value) {
    if (isXmlChar(ch.codePointAt(0) ?? 0)) {
      clean += ch;
    }
  }

  return clean
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&apos;");
}

const XML_HEADER = '<?xml version="1.0" encoding="UTF-8"?>';

export interface SitemapUrl {
  loc: string;
  lastmod?: string;
}

export function buildSitemapXml(urls: SitemapUrl[]): string {
  const rows = urls.map(
    (u) => `  <url>\n    <loc>${escapeXml(u.loc)}</loc>${u.lastmod ? `\n    <lastmod>${escapeXml(u.lastmod)}</lastmod>` : ""}\n  </url>`,
  );

  return `${XML_HEADER}\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${rows.join("\n")}\n</urlset>\n`;
}

/** Newest-first entries published within the Google News window, capped at the per-file limit. */
export function selectNewsSitemapEntries(entries: NewsSitemapEntry[], now: Date): NewsSitemapEntry[] {
  const earliest = now.getTime() - NEWS_SITEMAP_WINDOW_MS;
  return entries.filter((e) => new Date(e.publishedAtUtc).getTime() >= earliest).slice(0, NEWS_SITEMAP_MAX_URLS);
}

export function buildNewsSitemapXml(items: { loc: string; title: string; publishedAt: string }[]): string {
  const rows = items.map(
    (n) =>
      `  <url>\n    <loc>${escapeXml(n.loc)}</loc>\n    <news:news>\n      <news:publication>\n        <news:name>${escapeXml(SITE_NAME)}</news:name>\n        <news:language>${SITE_LANGUAGE}</news:language>\n      </news:publication>\n      <news:publication_date>${escapeXml(n.publishedAt)}</news:publication_date>\n      <news:title>${escapeXml(n.title)}</news:title>\n    </news:news>\n  </url>`,
  );

  return `${XML_HEADER}\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:news="http://www.google.com/schemas/sitemap-news/0.9">\n${rows.join("\n")}\n</urlset>\n`;
}
