import type { Metadata } from "next";
import { notFound, permanentRedirect } from "next/navigation";
import Link from "next/link";
import Image from "next/image";
import { getPublishedNewsById, type NewsRetraction } from "@/lib/news-api";
import { formatArabicDate, estimateReadMinutes } from "@/lib/format";
import { htmlToPlainText } from "@/lib/html";
import { CorrectionsBox } from "@/components/CorrectionsBox";
import { JsonLd } from "@/components/JsonLd";
import { articleDescription, articleUrl, buildArticleJsonLd, buildBreadcrumbJsonLd, toIsoDate } from "@/lib/seo";
import { EDITORIAL_BYLINE, SITE_NAME, absoluteUrl } from "@/lib/site";

// Always fresh: an unpublished article must stop resolving immediately.
export const dynamic = "force-dynamic";

function safeDecode(value: string): string {
  try {
    return decodeURIComponent(value);
  } catch {
    return value;
  }
}

/**
 * Per-article <title>, description, canonical URL and Open Graph data
 * (Slice 18, spec §16). Before this every article shared the site-wide title.
 * An API failure deliberately returns the default metadata rather than
 * "noindex": a transient outage must never de-index a live article.
 */
export async function generateMetadata({ params }: { params: Promise<{ id: string }> }): Promise<Metadata> {
  const { id } = await params;

  let result: Awaited<ReturnType<typeof getPublishedNewsById>>;
  try {
    result = await getPublishedNewsById(id);
  } catch {
    return {};
  }

  if (!result) {
    return { title: `المقال غير موجود — ${SITE_NAME}`, robots: { index: false, follow: false } };
  }

  if (result.outcome === "retracted") {
    return {
      title: `سحب الخبر — ${SITE_NAME}`,
      robots: { index: false, follow: false },
    };
  }

  const article = result.article;

  const description = article.seoDescription || articleDescription(article) || undefined;
  const canonical = article.canonicalUrl || absoluteUrl(articleUrl(article));
  const title = article.seoTitle || `${article.title} — ${SITE_NAME}`;

  return {
    title,
    description,
    robots: { index: !article.noIndex, follow: !article.noFollow },
    ...(canonical ? { alternates: { canonical } } : {}),
    openGraph: {
      type: "article",
      siteName: SITE_NAME,
      title: article.socialTitle || title,
      description,
      ...(article.socialImageUrl ? { images: [article.socialImageUrl] } : {}),
      ...(canonical ? { url: canonical } : {}),
      publishedTime: toIsoDate(article.publishedAtUtc),
      modifiedTime: toIsoDate(article.updatedAtUtc),
    },
    twitter: { card: (article.twitterImageUrl || article.socialImageUrl) ? "summary_large_image" : "summary", title: article.twitterTitle || article.socialTitle || title, description: article.twitterDescription || article.socialDescription || description,
      ...((article.twitterImageUrl || article.socialImageUrl) ? { images: [article.twitterImageUrl || article.socialImageUrl!] } : {}) },
  };
}

/**
 * Article page — Slice 8; since Slice 16 it renders a newsroom-PUBLISHED
 * article. Since Slice 21 the body is server-sanitized HTML (decision D2).
 * Originally Ported from the demo's article.html: breadcrumb,
 * article-head (eyebrow/subtitle/title/meta/share row), hero figure, and
 * article-body. Two deliberate departures from the literal demo markup:
 *   - No category/section exists in the Domain model yet, so the eyebrow
 *     breadcrumb link is omitted rather than showing a fake category.
 *   - .meta-city is rendered even though the demo's own inline <style>
 *     force-hides it (`display:none!important`) — that hide rule stays in
 *     the imported CSS untouched (not our call to reverse a client design
 *     decision), but showing city here specifically is intentionally
 *     different: Slice 6 makes real city data available for the first
 *     time, and a diaspora platform's whole premise is "where are our
 *     people" — so it's shown via .meta-item.meta-country instead of the
 *     suppressed .meta-city class, using the same visual treatment.
 */
export default async function ArticlePage({ params }: { params: Promise<{ id: string; slug?: string }> }) {
  const { id, slug } = await params;
  const result = await getPublishedNewsById(id);

  if (!result) {
    notFound();
  }

  if (result.outcome === "retracted") {
    const notice: NewsRetraction = result.notice;
    const retractedLabel = formatArabicDate(notice.retractedAtUtc);
    return (
      <main id="main" className="wrap retraction-notice" dir="rtl">
        <p className="article-eyebrow">إشعار تحريري</p>
        <h1>{notice.title}</h1>
        <p>سُحب هذا الخبر{retractedLabel ? ` في ${retractedLabel}` : ""}.</p>
        <p>{notice.notice}</p>
        <Link className="cms-btn" href="/">العودة إلى الصفحة الرئيسية</Link>
      </main>
    );
  }

  const story = result.article;
  // Next hands non-ASCII dynamic segments over percent-encoded (Arabic slugs are the norm here:
  // the backend builds them from the title). Comparing the raw param with the stored slug made
  // EVERY Arabic article "non-canonical" -> permanentRedirect to the very same URL -> endless
  // redirect loop and a blank page. Decode before comparing.
  if (!slug || safeDecode(slug).normalize("NFC") !== story.slug.normalize("NFC")) permanentRedirect(articleUrl(story));

  const publishedLabel = formatArabicDate(story.publishedAtUtc);
  const readMinutes = estimateReadMinutes(htmlToPlainText(story.body));
  const place = story.cityNameAr ?? story.countryNameAr ?? null;
  const byline = story.authorName || EDITORIAL_BYLINE;

  return (
    <main id="main">
      <JsonLd data={buildArticleJsonLd(story)} />
      <JsonLd
        data={buildBreadcrumbJsonLd([
          { name: "الرئيسية", path: "/" },
          { name: story.title, path: articleUrl(story) },
        ])}
      />
      <div className="wrap article-breadcrumb-wrap">
        <nav className="breadcrumb article-breadcrumb" aria-label="مسار الصفحة">
          <Link href="/" className="breadcrumb-home">
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path d="M3 10.8 12 3l9 7.8v9a1.2 1.2 0 0 1-1.2 1.2H4.2A1.2 1.2 0 0 1 3 19.8v-9Z" />
              <path d="M9 21v-6.2h6V21" />
            </svg>
            <span>الرئيسية</span>
          </Link>
        </nav>
      </div>

      <div className="article-head wrap">
        {story.isArchived && (
          <p className="article-archive-label" role="status">
            <span className="status-pill archived">أرشيف</span>
            {formatArabicDate(story.publishedAtUtc) && <span>نُشر في {formatArabicDate(story.publishedAtUtc)}</span>}
          </p>
        )}
        {story.summary && <p className="article-subtitle">{story.summary}</p>}
        <h1 className="article-title">{story.title}</h1>

        <div className="article-toolbar">
          <div className="article-meta">
            <span className="author">
              <span className="avatar" style={{ width: 26, height: 26, fontSize: 11 }}>ج</span>
              <span>{byline}</span>
            </span>
            {publishedLabel && (
              <span className="meta-item meta-date">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round">
                  <rect x="3" y="4.5" width="18" height="16" rx="2" />
                  <path d="M3 9.5h18" />
                  <path d="M8 3v3M16 3v3" />
                </svg>
                <span>{publishedLabel}</span>
              </span>
            )}
            {place && (
              <span className="meta-item meta-country">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round">
                  <path d="M20 10.5c0 6-8 11-8 11s-8-5-8-11a8 8 0 0 1 16 0Z" />
                  <circle cx="12" cy="10.5" r="2.6" />
                </svg>
                <span>{place}</span>
              </span>
            )}
            <span className="meta-item meta-read">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round">
                <circle cx="12" cy="12" r="9" />
                <path d="M12 7v5l3.2 2" />
              </svg>
              <span>{readMinutes} {readMinutes === 1 ? "دقيقة قراءة" : "دقائق قراءة"}</span>
            </span>
          </div>
        </div>
      </div>

      <figure className="article-hero-figure">
        {story.socialImageUrl ? (
          // Image URL is persisted by the newsroom's media/SEO workflow.
          // Keep the branded placeholder only when the article has no cover.
          <Image className="article-hero-img wrap" src={story.socialImageUrl} alt={story.title} width={1200} height={760} unoptimized />
        ) : <div className="article-hero-img wrap article-hero-placeholder" aria-hidden="true"><span>جسور</span></div>}
      </figure>

      <div className="wrap">
        <CorrectionsBox corrections={story.corrections.filter((c) => c.isMajor)} major />
        {/* story.body is HTML that the API sanitized against an allow-list BEFORE storing it
            (Slice 21, decision D2); legacy plain-text articles arrive HTML-encoded. Rendering
            it as markup is safe only because of that server-side boundary — never feed this
            element anything that did not come from the API's `body` field. */}
        <article className="article-body" dangerouslySetInnerHTML={{ __html: story.body }} />
        <CorrectionsBox corrections={story.corrections.filter((c) => !c.isMajor)} />
      </div>
    </main>
  );
}
