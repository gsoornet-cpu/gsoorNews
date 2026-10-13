import type { Metadata } from "next";
import Link from "next/link";
import Image from "next/image";
import { getPublishedNews, getPublishedVideos } from "@/lib/news-api";
import type { StorySummary } from "@/lib/stories-api";
import { JsonLd } from "@/components/JsonLd";
import { buildOrganizationJsonLd } from "@/lib/seo";
import { absoluteUrl } from "@/lib/site";
import { StoryGridCard } from "@/components/StoryGridCard";
import { HomeHeroSlider } from "@/components/home/HomeHeroSlider";
import { HomePollCard, NewsletterForm } from "@/components/home/HomePollCard";
import { ServicesBand } from "@/components/home/ServicesBand";
import { formatArabicDate } from "@/lib/format";

// The homepage is rendered per request so a newly published article (and a
// deleted one) is reflected immediately — no stale cache during the client trial.
export const dynamic = "force-dynamic";

export function generateMetadata(): Metadata {
  const canonical = absoluteUrl("/");
  return canonical ? { alternates: { canonical } } : {};
}

/**
 * Desk slug -> public label. The slugs are exactly the "presentation desks" an
 * editor ticks in the CMS (see EditorialDesk on the backend). A homepage section
 * shows ONLY published articles that carry that section's desk.
 */
const deskLabels: Record<string, string> = {
  mughtarib: "أخبار المغتربين", success: "قصص نجاح", egypt: "أخبار مصر", opportunities: "فرص استثمارية",
  official: "مع مسئول", events: "فعاليات", red: "خط أحمر", lamma: "اللمة الحلوة", secondgen: "الجيل الثاني",
  sports: "رياضة", arts: "فنون", articles: "مقالات رأي", various: "منوعات",
};

function articleHref(story: StorySummary) {
  return story.slug ? `/article/${story.id}/${encodeURIComponent(story.slug)}` : `/article/${story.id}`;
}

function placeOf(story: StorySummary) {
  return story.cityNameAr ?? story.countryNameAr ?? "جسور";
}

/** Section heading, same markup as the Final Demo (`.section-head` + `.eyebrow` link). */
function Head({ label, slug, more, asHeading }: { label: string; slug: string; more?: string; asHeading?: boolean }) {
  const href = `/category?cat=${slug}`;
  const eyebrow = <Link className="eyebrow" href={href}>{label}</Link>;
  return (
    <div className="section-head">
      {asHeading ? <h2>{eyebrow}</h2> : eyebrow}
      {more && <Link className="more" href={href}>{more}</Link>}
    </div>
  );
}

function EmptyDesk({ label }: { label: string }) {
  return <div className="home-empty-desk"><span aria-hidden="true">✦</span><p>لا توجد مواد منشورة في «{label}» حتى الآن.</p><span>ستظهر التغطيات هنا بعد نشر خبر واختيار هذا القسم.</span></div>;
}

/** A clearly marked, non-sticky sponsored placement that preserves the supplied creative. */
function HomeAdPlacement({ image, alt, size }: { image: string; alt: string; size: number }) {
  return (
    <aside className="home-ad-placement" aria-label="إعلان ممول">
      <div className="wrap">
        <div className="home-ad-placement__frame">
          <span className="home-ad-placement__label">مساحة إعلانية <span aria-hidden="true">·</span> إعلان ممول</span>
          <Image src={image} alt={alt} width={size} height={size} unoptimized loading="lazy" />
        </div>
      </div>
    </aside>
  );
}

/** Demo's `.six-grid` of `.grid-card`s (cardGrid). */
function DeskGrid({ stories, label, columns }: { stories: StorySummary[]; label: string; columns?: number }) {
  if (!stories.length) return <EmptyDesk label={label} />;
  return (
    <div className="six-grid" style={columns ? { gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))` } : undefined}>
      {stories.map((story) => <StoryGridCard key={story.id} story={story} />)}
    </div>
  );
}

function EditorialColumn({ slug, stories }: { slug: "red" | "official"; stories: StorySummary[] }) {
  const title = deskLabels[slug];
  const href = `/category?cat=${slug}`;
  return (
    <section className={`editorial-column editorial-column--${slug}`} aria-label={title}>
      <div className="editorial-column__head">
        <Link href={href} className="editorial-column__title"><span aria-hidden="true" />{title}</Link>
        <Link href={href} className="editorial-column__more">{slug === "red" ? "كل التحديثات ←" : "كل اللقاءات ←"}</Link>
      </div>
      {stories.length ? <>
        <div className="editorial-top-grid">
          {stories.slice(0, 2).map((story) => (
            <Link key={story.id} href={articleHref(story)} className="editorial-top-card">
              {story.imageUrl ? <Image src={story.imageUrl} alt="" width={720} height={480} unoptimized /> : <div className="editorial-top-card__image"><span>جسور</span></div>}
              <h3>{story.title}</h3>
            </Link>
          ))}
        </div>
        <div className="editorial-news-rows">
          {stories.slice(2, 4).map((story) => (
            <Link key={story.id} href={articleHref(story)} className="editorial-news-row">
              {story.imageUrl ? <Image src={story.imageUrl} alt="" width={224} height={156} unoptimized /> : <span className="editorial-news-row__ph" aria-hidden="true">جسور</span>}
              <span className="editorial-news-row__body">
                <h3>{story.title}</h3>
                <small>{placeOf(story)} · {formatArabicDate(story.publishedAtUtc)}</small>
              </span>
            </Link>
          ))}
        </div>
      </> : <EmptyDesk label={title} />}
    </section>
  );
}

/** Demo's cardLamma(): `.lamma-card` > `.thumb` + h4 + `.foot`. */
function LammaCard({ story }: { story: StorySummary }) {
  return (
    <Link className="lamma-card" href={articleHref(story)} aria-label={story.title}>
      <div className={`thumb${story.imageUrl ? "" : " thumb-placeholder"}`}>
        {story.imageUrl ? <Image src={story.imageUrl} alt="" width={720} height={480} unoptimized /> : <span>ج</span>}
      </div>
      <h4>{story.title}</h4>
      <div className="foot"><span>{placeOf(story)}</span><span>·</span><span>{formatArabicDate(story.publishedAtUtc)}</span></div>
    </Link>
  );
}

function LammaSection({ lamma, events }: { lamma: StorySummary[]; events: StorySummary[] }) {
  const [feature, ...rest] = lamma;
  return (
    <section className="section tight on-surface" id="lamma">
      <div className="wrap">
        <div className="split-2" style={{ gridTemplateColumns: "1fr 340px" }}>
          <div>
            <Head label={deskLabels.lamma} slug="lamma" more="كل اللمّات ←" />
            {feature ? <>
              <Link className="lead-story lamma-feature" href={articleHref(feature)}>
                <div className="lead-story-img">
                  {feature.imageUrl ? <Image src={feature.imageUrl} alt="" width={1200} height={760} unoptimized /> : <div className="home-hero-fallback" aria-hidden="true"><span>جسور</span></div>}
                  <span className="lamma-badge">🤝 لمّة الجالية</span>
                </div>
                <div className="lead-story-body">
                  <h2>{feature.title}</h2>
                  {feature.excerpt && <p className="sub">{feature.excerpt}</p>}
                  <div className="foot"><span>{placeOf(feature)}</span><span>·</span><span>{formatArabicDate(feature.publishedAtUtc)}</span></div>
                </div>
              </Link>
              {rest.length > 0 && <div className="lamma-grid">{rest.slice(0, 4).map((story) => <LammaCard key={story.id} story={story} />)}</div>}
            </> : <EmptyDesk label={deskLabels.lamma} />}
          </div>
          <aside>
            <div className="section-head"><h2 style={{ fontSize: 14 }}>📅 فعاليات وتغطيات</h2></div>
            <div className="events-widget">
              {events.slice(0, 4).map((story) => (
                <Link className="event-item" key={story.id} href={articleHref(story)}>
                  <span className="event-date"><b>{story.publishedAtUtc ? new Date(story.publishedAtUtc).getDate() : "—"}</b><span>{story.publishedAtUtc ? new Date(story.publishedAtUtc).toLocaleDateString("ar-EG", { month: "short" }) : ""}</span></span>
                  <span><h5>{story.title}</h5><span className="foot">{placeOf(story)} · {formatArabicDate(story.publishedAtUtc)}</span></span>
                </Link>
              ))}
              {events.length === 0 && <EmptyDesk label={deskLabels.events} />}
              <Link href="/category?cat=events" className="cta">شاهد كل الفعاليات ←</Link>
            </div>
            <div className="ad-slot slim"><span className="lbl">مساحة إعلانية · Sponsored</span><div>ضع إعلانك هنا</div></div>
          </aside>
        </div>
      </div>
    </section>
  );
}

function AtmaenFeature() {
  return (
    <section className="section" id="atmaen"><div className="wrap"><div className="split-2" style={{ gridTemplateColumns: "1fr 1fr", alignItems: "center" }}>
      <div><span className="eyebrow">أهم ميزة في المنصة</span><h2 style={{ fontSize: 23, margin: "14px 0 12px", lineHeight: 1.6 }}>لما تحصل أزمة كبيرة، مش لازم تستنى الأخبار — ابدأ حالة اطمّن فوراً</h2>
        <p className="text-mute" style={{ fontSize: 14, lineHeight: 1.85 }}>نظام حالات خاص بين مقدّم الطلب وشبكة استجابة موثوقة. بيانات الحالات لا تُعرض للعامة، والوصول محدود بالأدوار والصلاحيات.</p>
        <div className="flex gap-8 mt-24"><Link className="btn btn-primary" href="/category?cat=mughtarib">تعرّف على أخبار المغتربين</Link><Link className="btn btn-ghost" href="/category?cat=red">آخر التحديثات ←</Link></div>
      </div>
      <div className="cms-panel" style={{ margin: 0 }}><div className="status-line active"><span className="dot" /> اختيار الدولة والمدينة</div><div className="status-line"><span className="dot" /> وصف مختصر للحالة</div><div className="status-line"><span className="dot" /> تحقق سريع بـ OTP</div><div className="status-line"><span className="dot" /> تعيين لشبكة الاستجابة الأقرب</div><div className="result-note">جسور أداة معلومات وتواصل، وليست جهة طوارئ رسمية.</div></div>
    </div></div></section>
  );
}

function NewsRecirculation({ desks }: { desks: Record<string, StorySummary[]> }) {
  const sections = ["arts", "sports", "various"];
  return (
    <section className="section tight recirc-section"><div className="wrap"><div className="recirc-cols">{sections.map((slug) => {
      const label = deskLabels[slug];
      const [lead, ...rest] = desks[slug] ?? [];
      return <div className="recirc-col" key={slug}><Link href={`/category?cat=${slug}`} className="eyebrow">{label}</Link>
        {lead ? <><Link className="recirc-feature" href={articleHref(lead)}><div className="thumb">{lead.imageUrl ? <Image src={lead.imageUrl} alt="" width={720} height={440} unoptimized /> : <span>جسور</span>}</div><h4>{lead.title}</h4><div className="foot">{placeOf(lead)} · {formatArabicDate(lead.publishedAtUtc)}</div></Link>
          <div className="articles-list">{rest.slice(0, 3).map((story) => <Link className="row-card" href={articleHref(story)} key={story.id}><div className="thumb">{story.imageUrl && <Image src={story.imageUrl} alt="" width={200} height={140} unoptimized />}</div><div><h4>{story.title}</h4><div className="foot">{placeOf(story)} · {formatArabicDate(story.publishedAtUtc)}</div></div></Link>)}</div></>
        : <EmptyDesk label={label} />}
      </div>;
    })}</div></div></section>
  );
}

export default async function HomePage() {
  const deskSlugs = Object.keys(deskLabels);
  const [newsResult, videoResult, ...deskResults] = await Promise.allSettled([
    getPublishedNews(1, 50),
    getPublishedVideos(1, 12), // Include every published article whose video was selected in the article editor.
    ...deskSlugs.map((slug) => getPublishedNews(1, 10, slug)),
  ]);
  const feed = newsResult.status === "fulfilled" ? newsResult.value.items : [];
  const videos = videoResult.status === "fulfilled" ? videoResult.value.items : [];
  const desks: Record<string, StorySummary[]> = Object.fromEntries(deskSlugs.map((slug, index) => {
    const result = deskResults[index];
    return [slug, result?.status === "fulfilled" ? result.value.items : []];
  }));

  // Only videos explicitly selected on their articles are returned by the videos API.
  const heroSlides = videos.map((video) => ({
    id: video.id, title: video.title, excerpt: video.excerpt, href: `/article/${video.id}/${encodeURIComponent(video.slug)}`,
    imageUrl: video.thumbnailUrl, videoUrl: video.videoUrl, publishedAtUtc: video.publishedAtUtc,
  }));

  const latest = feed.slice(0, 6);

  // Section order and markup mirror the approved Final Demo's home page. The demo's
  // video rail, "خط أحمر" band, UAE strip and sports band are hidden/commented out
  // there, so they are intentionally not rendered here either.
  return (
    <main id="main">
      <JsonLd data={buildOrganizationJsonLd()} />
      {heroSlides.length > 0 && <HomeHeroSlider slides={heroSlides} />}
      <HomeAdPlacement image="/assets/advertisements/nbe-project-finance.png" alt="إعلان البنك الأهلي المصري: موّل مشروعك وكبّر حلمك" size={1600} />

      <section className="section tight on-surface">
        <div className="wrap">
          <Head label={deskLabels.mughtarib} slug="mughtarib" />
          <DeskGrid stories={desks.mughtarib.slice(0, 6)} label={deskLabels.mughtarib} />
        </div>
      </section>
      <HomeAdPlacement image="/assets/advertisements/cib-swype.png" alt="إعلان CIB Swype: وفّر دلوقتي وقسّط براحتك" size={1080} />
      <ServicesBand />
      <AtmaenFeature />

      {/* الويب يحتفظ بمساحة الإعلان الجانبية؛ إعلانات الموبايل تظهر بعد السكشن. */}
      <section className="section on-surface">
        <div className="wrap">
          <div className="split-2">
            <aside className="sidebar-interactive-ad home-success-aside">
              <HomePollCard />
              <div className="ad-slot-banner home-success-desktop-ad">
                <span className="ad-label">مساحة إعلانية · Sponsored</span>
                <div className="ad-placeholder"><span>مساحة إعلانية متجاوبة</span></div>
              </div>
            </aside>
            <div className="home-success-stories">
              <Head label={deskLabels.success} slug="success" asHeading />
              <DeskGrid stories={desks.success.slice(0, 4)} label={deskLabels.success} columns={2} />
            </div>
          </div>
        </div>
      </section>
      <HomeAdPlacement image="/assets/advertisements/banque-misr-certificate.png" alt="إعلان بنك مصر: شهادة القمة بعائد سنوي ثابت يُصرف شهريًا" size={1600} />

      <section className="section tight on-surface">
        <div className="wrap">
          <Head label={deskLabels.egypt} slug="egypt" />
          <DeskGrid stories={desks.egypt.slice(0, 6)} label={deskLabels.egypt} />
        </div>
      </section>

      <section className="section editorial-columns-section" aria-label="خط أحمر ومع مسؤول">
        <div className="wrap">
          <div className="editorial-columns">
            <EditorialColumn slug="red" stories={desks.red.slice(0, 4)} />
            <EditorialColumn slug="official" stories={desks.official.slice(0, 4)} />
          </div>
        </div>
      </section>

      <section className="on-ink section tight opinion-section" id="opinion">
        <div className="wrap">
          <Head label={deskLabels.articles} slug="articles" more="كل المقالات ←" />
          {desks.articles.length ? (
            <div className="opinion-grid">
              {desks.articles.slice(0, 8).map((story, index) => (
                <article className={`opinion-tile${index === 0 ? " featured" : ""}`} key={story.id}>
                  <div className="opinion-tile-title"><Link href={articleHref(story)}>{story.title}</Link></div>
                </article>
              ))}
            </div>
          ) : <EmptyDesk label={deskLabels.articles} />}
        </div>
      </section>

      <section className="section on-surface opportunities-section">
        <div className="wrap">
          <div className="split-2">
            <div>
              <Head label={deskLabels.opportunities} slug="opportunities" />
              <DeskGrid stories={desks.opportunities.slice(0, 4)} label={deskLabels.opportunities} columns={2} />
            </div>
            <aside>
              <div className="section-head"><h2><span className="eyebrow">أحدث الأخبار</span></h2></div>
              <div className="rank-list">
                {latest.map((story, index) => (
                  <Link className={`item home-rank-item${index < 3 ? " top" : ""}`} href={articleHref(story)} key={story.id}>
                    <span className="rank">{String(index + 1).padStart(2, "0")}</span>
                    <div><h5>{story.title}</h5></div>
                  </Link>
                ))}
              </div>
            </aside>
          </div>
        </div>
      </section>

      <section className="section" id="second-gen">
        <div className="wrap">
          <Head label={deskLabels.secondgen} slug="secondgen" asHeading />
          <DeskGrid stories={desks.secondgen.slice(0, 4)} label={deskLabels.secondgen} columns={4} />
        </div>
      </section>

      <LammaSection lamma={desks.lamma} events={desks.events} />

      <section className="section on-surface" id="city-alerts" style={{ paddingTop: 0 }}>
        <div className="wrap">
          <div className="cta-banner">
            <div><h2>ما تفوّتش أي تحديث يخص بلدك</h2><p>اشترك في تنبيهات المدينة اللي إنت أو أهلك فيها — إشعار لحظي وقت الأزمات فقط، مفيش إزعاج يومي.</p></div>
            <NewsletterForm />
          </div>
        </div>
      </section>

      <NewsRecirculation desks={desks} />
    </main>
  );
}
