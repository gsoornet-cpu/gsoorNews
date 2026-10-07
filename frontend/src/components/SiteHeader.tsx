import { HeaderInteractive } from "./HeaderInteractive";
import { getPublishedNews } from "@/lib/news-api";

/**
 * Server Component so the ticker's headlines are real titles of
 * newsroom-PUBLISHED articles (Slice 16 — previously ingested, unreviewed
 * Stories) fetched at render time — the demo's ticker is 6 fixed sample headlines
 * baked into index.html; this fetches the actual latest ones instead.
 * Fails open: if the API call throws (backend down, etc.), the ticker is
 * simply omitted rather than crashing the whole page's header via the
 * error boundary — a nav bar should never go down because a background
 * data call failed.
 */
export async function SiteHeader() {
  let tickerHeadlines: string[] = [];

  try {
    const { items } = await getPublishedNews(1, 6);
    tickerHeadlines = items.map((s) => s.title);
  } catch {
    tickerHeadlines = [];
  }

  return <HeaderInteractive tickerHeadlines={tickerHeadlines} />;
}
