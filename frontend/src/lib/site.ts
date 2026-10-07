// Site identity and absolute-URL helpers for SEO output (canonical URLs,
// Schema.org JSON-LD, sitemaps). Slice 18, spec §16.
//
// SITE_URL is the public origin of the website (e.g. https://jusoor.example).
// It is read at request time on the server (pages using it are dynamic), so
// it is a runtime setting, not a build-time one. In development it defaults
// to http://localhost:3000. In production it is REQUIRED: when it is missing
// or invalid, canonical URLs / structured data are omitted and the sitemap
// routes answer 503, rather than publishing wrong (localhost) absolute URLs
// that search engines would then trust.

export const SITE_NAME = "جسور";
export const SITE_LANGUAGE = "ar";
export const LOGO_PATH = "/assets/josour-logo.png";

/** Public byline — the same text the article page displays. No individual author profiles exist yet. */
export const EDITORIAL_BYLINE = "هيئة تحرير جسور";

let warned = false;

function warnOnce(message: string): void {
  if (!warned) {
    warned = true;
    console.warn(`[seo] ${message}`);
  }
}

/** The site's origin without a trailing slash, or null when it is not (validly) configured. */
export function getSiteUrl(): string | null {
  const raw = process.env.SITE_URL?.trim();

  if (raw) {
    try {
      const url = new URL(raw);
      if (url.protocol === "http:" || url.protocol === "https:") {
        return url.origin;
      }
    } catch {
      // fall through to the warning below
    }

    warnOnce("SITE_URL is not a valid http(s) URL — canonical URLs, structured data and sitemaps are disabled.");
    return null;
  }

  if (process.env.NODE_ENV !== "production") {
    return "http://localhost:3000";
  }

  warnOnce("SITE_URL is not set — canonical URLs, structured data and sitemaps are disabled.");
  return null;
}

/** Absolute URL for a site path, or null when SITE_URL is not configured. */
export function absoluteUrl(path: string): string | null {
  const base = getSiteUrl();
  return base ? `${base}${path.startsWith("/") ? path : `/${path}`}` : null;
}
