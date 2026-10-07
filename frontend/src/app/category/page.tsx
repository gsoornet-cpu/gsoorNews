import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { StoryGridCard } from "@/components/StoryGridCard";
import { PRESENTATION_DESKS } from "@/lib/cms-client";
import { getPublishedNews } from "@/lib/news-api";
import { absoluteUrl } from "@/lib/site";

export const dynamic = "force-dynamic";
const PAGE_SIZE = 18;

type SearchParams = Promise<{ cat?: string | string[]; page?: string | string[] }>;

async function resolveDesk(searchParams: SearchParams) {
  const params = await searchParams;
  const slug = typeof params.cat === "string" ? params.cat : null;
  return PRESENTATION_DESKS.find((desk) => desk.slug === slug) ?? null;
}

export async function generateMetadata({ searchParams }: { searchParams: SearchParams }): Promise<Metadata> {
  const desk = await resolveDesk(searchParams);
  if (!desk) return { title: "القسم غير موجود" };
  const canonical = absoluteUrl(`/category?cat=${desk.slug}`);
  return { title: desk.label, ...(canonical ? { alternates: { canonical } } : {}) };
}

export default async function CategoryPage({ searchParams }: { searchParams: SearchParams }) {
  const params = await searchParams;
  const rawPage = typeof params.page === "string" ? Number(params.page) : 1;
  const page = Number.isSafeInteger(rawPage) && rawPage > 0 ? rawPage : 1;
  const desk = await resolveDesk(searchParams);
  if (!desk) notFound();

  let result: Awaited<ReturnType<typeof getPublishedNews>>;
  try {
    result = await getPublishedNews(page, PAGE_SIZE, desk.slug);
  } catch {
    return <main id="main"><div className="wrap"><div className="state-panel" role="alert"><h1>تعذّر تحميل القسم</h1><p>تعذّر الاتصال بالخادم. حاول تحديث الصفحة بعد قليل.</p></div></div></main>;
  }

  return (
    <main id="main">
      <section className="page-header category-page-header">
        <div className="wrap">
          <nav className="breadcrumb" aria-label="مسار الصفحة">
            <Link className="breadcrumb-home" href="/">الرئيسية</Link>
            <span className="breadcrumb-separator" aria-hidden="true">›</span>
            <Link className="breadcrumb-section" href="/">الأخبار</Link>
            <span className="breadcrumb-separator" aria-hidden="true">›</span>
            <span aria-current="page">{desk.label}</span>
          </nav>
          <div className="category-heading">
            <div className="category-heading-line" aria-hidden="true" />
            <div className="category-heading-content">
              <h1>{desk.label}</h1>
              <p className="category-heading-meta">{result.totalCount} خبرًا منشورًا</p>
            </div>
          </div>
        </div>
      </section>
      <section className="section tight on-surface" style={{ paddingTop: 0 }}>
        <div className="wrap">
          {result.items.length === 0 ? (
            <p className="empty-state" style={{ padding: "40px 0", textAlign: "center", color: "var(--text-mute)" }}>
              <strong>لا توجد أخبار في هذا القسم حاليًا.</strong><br />ستظهر هنا الأخبار بعد نشرها وتحديد هذا القسم لها.
            </p>
          ) : (
            <div className="six-grid">
              {result.items.map((story) => <StoryGridCard key={story.id} story={story} />)}
            </div>
          )}
          {result.totalPages > 1 && (
            <nav className="pagination" aria-label="صفحات القسم">
              {page > 1 && <Link href={`/category?cat=${desk.slug}&page=${page - 1}`} rel="prev" aria-label="الصفحة السابقة">‹</Link>}
              {Array.from({ length: result.totalPages }, (_, index) => index + 1)
                .filter((number) => result.totalPages <= 7 || number === 1 || number === result.totalPages || Math.abs(number - page) <= 1)
                .map((number, index, visible) => (
                  <span key={number} style={{ display: "contents" }}>
                    {index > 0 && number - visible[index - 1] > 1 && <span aria-hidden="true">…</span>}
                    <Link href={`/category?cat=${desk.slug}&page=${number}`} aria-current={number === page ? "page" : undefined}>{number}</Link>
                  </span>
                ))}
              {page < result.totalPages && <Link href={`/category?cat=${desk.slug}&page=${page + 1}`} rel="next" aria-label="الصفحة التالية">›</Link>}
            </nav>
          )}
        </div>
      </section>
    </main>
  );
}
