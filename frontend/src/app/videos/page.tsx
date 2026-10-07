import type { Metadata } from "next";
import Link from "next/link";
import { getPublishedVideos } from "@/lib/news-api";
import { formatArabicDate } from "@/lib/format";
import { absoluteUrl, SITE_NAME } from "@/lib/site";

export const dynamic = "force-dynamic";

export function generateMetadata(): Metadata {
  const canonical = absoluteUrl("/videos");
  return {
    title: `جسور فيديو — ${SITE_NAME}`,
    description: "شاهد أحدث الفيديوهات المنشورة من تحرير جسور.",
    ...(canonical ? { alternates: { canonical } } : {}),
  };
}

export default async function VideosPage({ searchParams }: { searchParams: Promise<{ page?: string }> }) {
  const params = await searchParams;
  const page = Number(params.page ?? "1");
  const safePage = Number.isSafeInteger(page) && page > 0 ? page : 1;
  let result: Awaited<ReturnType<typeof getPublishedVideos>> | null = null;
  try {
    result = await getPublishedVideos(safePage, 18);
  } catch {
    return <main id="main"><div className="wrap"><div className="state-panel"><h2>تعذّر تحميل الفيديوهات</h2><p>حدثت مشكلة في الاتصال بالخادم. حاول تحديث الصفحة بعد قليل.</p></div></div></main>;
  }

  const [featured, ...videos] = result.items;
  if (!featured) {
    return <main id="main"><section className="section"><div className="wrap"><div className="section-head"><span className="eyebrow">جسور فيديو</span><h1>فيديوهات جسور</h1></div><div className="state-panel"><h2>لا توجد فيديوهات منشورة حالياً</h2><p>ستظهر هنا الفيديوهات التي يختارها فريق التحرير وينشرها.</p></div></div></section></main>;
  }

  return (
    <main id="main">
      <section className="section">
        <div className="wrap">
          <div className="section-head"><span className="eyebrow">جسور فيديو</span><h1>فيديوهات جسور</h1></div>
          <article className="video-featured">
            <div className="video-featured-media"><video src={featured.videoUrl} controls playsInline preload="metadata" aria-label={featured.title} /></div>
            <div className="video-featured-body"><span className="eyebrow">الأحدث</span><h2>{featured.title}</h2>{featured.excerpt && <p>{featured.excerpt}</p>}</div>
          </article>
          <div className="video-featured-meta">
            <time dateTime={featured.publishedAtUtc}>{formatArabicDate(featured.publishedAtUtc)}</time>
            {featured.credit && <span>{featured.credit}</span>}
            <Link href={`/article/${featured.id}/${encodeURIComponent(featured.slug)}`}>اقرأ الخبر المرتبط</Link>
          </div>

          {videos.length > 0 && <>
            <div className="section-head video-list-head"><span className="eyebrow">منشور حديثاً</span><h2>كل الفيديوهات</h2></div>
            <div className="video-grid">
              {videos.map((video) => <article className="video-card" key={video.id}>
                <div className="thumb video-card-player"><video src={video.videoUrl} controls playsInline preload="metadata" aria-label={video.title} /></div>
                <div className="video-card-body"><h3><Link href={`/article/${video.id}/${encodeURIComponent(video.slug)}`}>{video.title}</Link></h3>
                  {video.excerpt && <p>{video.excerpt}</p>}
                  <div className="video-card-meta"><time dateTime={video.publishedAtUtc}>{formatArabicDate(video.publishedAtUtc)}</time>{video.credit && <span>{video.credit}</span>}</div>
                </div>
              </article>)}
            </div>
          </>}

          {result.totalPages > 1 && <nav className="video-pagination" aria-label="صفحات الفيديوهات">
            {safePage > 1 && <Link className="cms-btn" href={`/videos?page=${safePage - 1}`}>الأحدث</Link>}
            <span>صفحة {result.page} من {result.totalPages}</span>
            {safePage < result.totalPages && <Link className="cms-btn" href={`/videos?page=${safePage + 1}`}>الأقدم</Link>}
          </nav>}
        </div>
      </section>
    </main>
  );
}
