"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  cms,
  workflowHints,
  STATUS_META,
  CmsError,
  type ArticleStatus,
  type CmsArticleSummary,
  type CmsSession,
  type WorkflowAction,
} from "@/lib/cms-client";
import { formatArabicDate } from "@/lib/format";

export default function CmsDashboardPage() {
  const router = useRouter();
  const [session, setSession] = useState<CmsSession | null>(null);
  const [items, setItems] = useState<CmsArticleSummary[] | null>(null);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [status, setStatus] = useState<ArticleStatus | "">("");
  const [searchTerm, setSearchTerm] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const requestSequence = useRef(0);

  const load = useCallback(
    async (filter: ArticleStatus | "", requestedPage: number, query = "") => {
      const requestId = ++requestSequence.current;
      setLoading(true);
      try {
        const result = await cms.list(filter || undefined, requestedPage, query);
        if (requestId !== requestSequence.current) return;
        setItems(result.items);
        setPage(result.page);
        setTotalCount(result.totalCount);
        setTotalPages(result.totalPages);
        setError(null);
      } catch (e) {
        if (requestId !== requestSequence.current) return;
        if (e instanceof CmsError && e.status === 401) {
          router.replace("/cms/login");
          return;
        }
        setItems([]);
        setError(e instanceof CmsError ? e.message : "تعذّر تحميل المقالات.");
      } finally {
        if (requestId === requestSequence.current) setLoading(false);
      }
    },
    [router],
  );

  useEffect(() => {
    void (async () => {
      const current = await cms.session();
      if (!current.authenticated) {
        router.replace("/cms/login");
        return;
      }
      setSession(current);
      if (current.roles?.length && current.roles.every((role) => role === "SeoEditor")) {
        setItems([]);
      }
    })();
  }, [router]);

  useEffect(() => {
    if (!session || session.roles?.length && session.roles.every((role) => role === "SeoEditor")) return;
    const timer = window.setTimeout(() => void load(status, page, searchTerm.trim()), searchTerm.trim() ? 280 : 0);
    return () => window.clearTimeout(timer);
  }, [session, status, page, searchTerm, load]);

  // One quick action per row: the next forward step of the workflow.
  // Requesting a revision needs a reason, so it lives in the article editor.
  const QUICK: Partial<Record<ArticleStatus, { action: WorkflowAction; label: string; style: string }>> = {
    Draft: { action: "submit", label: "إرسال للمراجعة", style: "primary" },
    InReview: { action: "approve", label: "اعتماد", style: "success" },
    Approved: { action: "publish", label: "نشر", style: "success" },
    Published: { action: "unpublish", label: "إلغاء النشر", style: "danger" },
  };

  async function runQuick(article: CmsArticleSummary) {
    const action = QUICK[article.status]?.action;
    if (!action) return;
    setBusyId(article.id);
    setError(null);
    try {
      switch (action) {
        case "submit":
          await cms.submit(article.id);
          break;
        case "approve":
          await cms.approve(article.id);
          break;
        case "publish":
          await cms.publish(article.id);
          break;
        default:
          await cms.unpublish(article.id);
      }
      await load(status, page, searchTerm.trim());
    } catch (e) {
      setError(e instanceof CmsError ? e.message : "تعذّر تنفيذ الإجراء.");
    } finally {
      setBusyId(null);
    }
  }

  async function logout() {
    await cms.logout();
    router.replace("/cms/login");
  }

  if (!session) {
    return <p>جارٍ التحميل…</p>;
  }

  return (
    <div className="cms-card">
      <div className="cms-head">
        <h1>إدارة الأخبار</h1>
        <div className="cms-actions">
          {session.roles?.includes("SeoEditor") && <Link className="cms-btn" href="/cms/seo">تحرير SEO</Link>}
          {session.roles?.some((role) => ["SeniorEditor", "ManagingEditor", "EditorInChief"].includes(role)) && <Link className="cms-btn" href="/cms/home-videos">فيديوهات الرئيسية</Link>}
          {session.roles?.some((role) => ["SeniorEditor", "ManagingEditor", "EditorInChief"].includes(role)) && <Link className="cms-btn" href="/cms/advertisements">إدارة الإعلانات</Link>}
          <div className="cms-filter">
            <label htmlFor="status" className="cms-hint">
              الحالة
            </label>
            <select
              id="status"
              value={status}
              onChange={(e) => {
                const value = e.target.value as ArticleStatus | "";
                setStatus(value);
                setPage(1);
              }}
            >
              <option value="">الكل</option>
              {(Object.keys(STATUS_META) as ArticleStatus[]).map((s) => (
                <option key={s} value={s}>
                  {STATUS_META[s].label}
                </option>
              ))}
            </select>
          </div>
          <Link className="cms-btn primary" href="/cms/articles/new">
            + مقال جديد
          </Link>
          <button className="cms-btn" type="button" onClick={logout}>
            تسجيل الخروج
          </button>
        </div>
      </div>

      {session.email && <p className="cms-hint">مسجّل الدخول: {session.email}</p>}

      {!(session.roles?.length && session.roles.every((role) => role === "SeoEditor")) && <div className="cms-news-toolbar">
        <label className="cms-news-search">
          <span aria-hidden="true">⌕</span>
          <input type="search" value={searchTerm} maxLength={120} onChange={(event) => { setSearchTerm(event.target.value); setPage(1); }} placeholder="ابحث في عنوان الخبر أو ملخصه" aria-label="البحث في الأخبار" />
        </label>
        <span className="cms-hint cms-news-results" aria-live="polite">{loading ? "جارٍ البحث…" : searchTerm.trim() ? `${totalCount} نتيجة بحث` : `${totalCount} مقالًا`}</span>
      </div>}

      {error && (
        <div className="cms-msg error" role="alert">
          {error}
        </div>
      )}

      {items === null ? (
        <p>جارٍ تحميل المقالات…</p>
      ) : items.length === 0 ? (
        <p className="cms-news-empty">{searchTerm.trim() ? "لا توجد أخبار تطابق كلمات البحث. جرّب كلمات أخرى." : status ? "لا توجد أخبار بهذه الحالة." : "لا توجد مقالات بعد. ابدأ بإنشاء مقال جديد."}</p>
      ) : (
        <>
        <p className="cms-hint">{searchTerm.trim() ? `${totalCount} نتيجة بحث · الصفحة ${page} من ${totalPages}` : `${totalCount} مقالًا · الصفحة ${page} من ${totalPages}`}</p>
        <table className="cms-table">
          <thead>
            <tr>
              <th>العنوان</th>
              <th>الحالة</th>
              <th>آخر تعديل</th>
              <th>إجراءات</th>
            </tr>
          </thead>
          <tbody>
            {items.map((a) => {
              const quick = QUICK[a.status];
              return (
              <tr key={a.id}>
                <td data-label="العنوان">
                  <Link href={`/cms/articles/${a.id}`}>{a.title}</Link>
                  {a.status === "Scheduled" && a.scheduledPublishAtUtc && (
                    <small className="cms-hint" style={{ display: "block" }}>
                      موعد النشر: {formatArabicDate(a.scheduledPublishAtUtc)}
                    </small>
                  )}
                </td>
                <td data-label="الحالة">
                  <span className={`cms-badge ${STATUS_META[a.status]?.badge ?? "draft"}`}>
                    {STATUS_META[a.status]?.label ?? a.status}
                  </span>
                </td>
                <td data-label="آخر تعديل">{formatArabicDate(a.lastModifiedAtUtc ?? a.createdAtUtc)}</td>
                <td data-label="إجراءات">
                  <div className="cms-actions">
                    <Link className="cms-btn" href={`/cms/articles/${a.id}`}>
                      تحرير
                    </Link>
                    {quick && workflowHints(session.roles, a.status).includes(quick.action) && (
                      <button
                        className={`cms-btn ${quick.style}`}
                        type="button"
                        disabled={busyId === a.id}
                        onClick={() => runQuick(a)}
                      >
                        {quick.label}
                      </button>
                    )}
                  </div>
                </td>
              </tr>
              );
            })}
          </tbody>
        </table>
        <div className="cms-actions" style={{ justifyContent: "center", marginTop: 18 }}>
          <button className="cms-btn" type="button" disabled={loading || page <= 1} onClick={() => setPage((current) => Math.max(1, current - 1))}>السابق</button>
          <span className="cms-hint">{page} / {totalPages}</span>
          <button className="cms-btn" type="button" disabled={loading || page >= totalPages} onClick={() => setPage((current) => Math.min(totalPages, current + 1))}>التالي</button>
        </div>
        </>
      )}
    </div>
  );
}
