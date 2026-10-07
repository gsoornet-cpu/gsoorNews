import type { Metadata } from "next";
import { getPublishedNews } from "@/lib/news-api";
import { StoryGridCard } from "@/components/StoryGridCard";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "البحث — جسور" };

export default async function SearchPage({ searchParams }: { searchParams: Promise<{ q?: string }> }) {
  const { q: rawQuery } = await searchParams;
  const query = rawQuery?.trim().slice(0, 120) ?? "";
  const result = query.length >= 2 ? await getPublishedNews(1, 30, undefined, query) : null;

  return (
    <main id="main" className="wrap search-results-page" dir="rtl">
      <section className="search-hero">
        <span className="search-hero-kicker">اكتشف التغطيات</span>
        <h1>ابحث في أخبار جسور</h1>
        <form action="/search" method="get" className="search-bar-lg">
          <input type="search" name="q" defaultValue={query} placeholder="ابحث عن خبر، دولة، أو موضوع…" aria-label="كلمات البحث" minLength={2} maxLength={120} />
          <button type="submit">بحث</button>
        </form>
      </section>
      {query.length < 2 ? <p className="search-empty">اكتب كلمتين على الأقل لبدء البحث في الأخبار المنشورة.</p> : (
        <section className="section tight">
          <div className="section-head"><h2>نتائج البحث عن «{query}»</h2><span>{result?.totalCount ?? 0} نتيجة</span></div>
          {result?.items.length ? <div className="six-grid">{result.items.map((story) => <StoryGridCard key={story.id} story={story} />)}</div> : <p className="search-empty">لم نعثر على نتائج مطابقة. جرّب كلمات أخرى.</p>}
        </section>
      )}
    </main>
  );
}
