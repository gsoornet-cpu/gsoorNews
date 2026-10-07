"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { cms, CmsError, type CmsSeoArticleSummary } from "@/lib/cms-client";

export default function SeoArticlesPage() {
  const router = useRouter();
  const [items, setItems] = useState<CmsSeoArticleSummary[]>([]);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [error, setError] = useState("");
  const load = useCallback(() => { void cms.listSeoArticles(page).then(result => { setItems(result.items); setTotalCount(result.totalCount); }).catch((e: unknown) => {
    if (e instanceof CmsError && e.status === 401) router.replace("/cms/login");
    else setError(e instanceof Error ? e.message : "تعذر تحميل قائمة المقالات.");
  }); }, [page, router]);
  useEffect(() => { load(); }, [load]);
  return <main className="cms-card" dir="rtl"><div className="cms-head"><h1>تحسين ظهور المقالات</h1><Link className="cms-btn" href="/cms">لوحة الأخبار</Link></div>
    {error && <div className="cms-msg error" role="alert">{error}</div>}
    {!items.length && !error ? <p>جارٍ تحميل المقالات…</p> : <ul>{items.map(article => <li key={article.id}>
      <Link href={`/cms/seo/articles/${article.id}`}>{article.title}</Link> — {article.status}
      {article.noIndex && <span className="status-pill">غير مفهرس</span>}
      {!article.primaryCategoryId && <span className="cms-hint"> يحتاج تصنيفًا رئيسيًا</span>}
    </li>)}</ul>}
    <div className="cms-actions"><button type="button" className="cms-btn" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>السابق</button>
      <span>{page} / {Math.max(1, Math.ceil(totalCount / 50))} — {totalCount} مقالًا</span>
      <button type="button" className="cms-btn" disabled={page >= Math.ceil(totalCount / 50)} onClick={() => setPage(p => p + 1)}>التالي</button></div>
  </main>;
}
