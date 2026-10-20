"use client";

import { useCallback, useEffect, useState } from "react";
import { cms } from "@/lib/cms-client";

type Report = { rangeDays: number; totalArticles: number; createdInRange: number; publishedInRange: number; updatedInRange: number; statusCounts: { status: string; count: number }[]; recentActivity: { title: string; fromStatus: string; toStatus: string; actorRoles: string; occurredAtUtc: string }[] };
const STATUS: Record<string, string> = { Draft: "مسودة", InReview: "قيد المراجعة", Approved: "معتمد", Published: "منشور", Scheduled: "مجدول", Retracted: "مسحوب", Archived: "مؤرشف" };
const ROLE: Record<string, string> = { Reporter: "محرر", AiEditor: "محرر مساعد", CopyEditor: "مدقق لغوي", FactChecker: "مدقق حقائق", SeniorEditor: "محرر أول", ManagingEditor: "مدير التحرير", EditorInChief: "رئيس التحرير", SystemAdmin: "مسؤول النظام" };

export default function EditorialMonitoringPage() {
  const [report, setReport] = useState<Report | null>(null);
  const [days, setDays] = useState(30);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [allowed, setAllowed] = useState(false);
  const load = useCallback(async () => {
    setLoading(true); setError("");
    try {
      const session = await cms.session();
      const hasAccess = (session.roles ?? []).some((role) => ["SeniorEditor", "ManagingEditor", "EditorInChief", "SystemAdmin"].includes(role));
      setAllowed(hasAccess);
      if (!hasAccess) return;
      const response = await fetch(`/cms-api/reports/editorial?days=${days}`, { credentials: "same-origin", cache: "no-store" });
      if (!response.ok) throw new Error(response.status === 403 ? "ليس لديك صلاحية عرض هذا التقرير." : "تعذّر تحميل تقرير المراقبة.");
      setReport(await response.json() as Report);
    } catch (reason) { setError(reason instanceof Error ? reason.message : "تعذّر تحميل التقرير."); }
    finally { setLoading(false); }
  }, [days]);
  useEffect(() => { void load(); }, [load]);

  return <div className="cms-page-stack">
    <section className="cms-page-heading"><div><span className="cms-eyebrow">متابعة سير العمل التحريري</span><h1>مراقبة غرفة الأخبار</h1><p>صورة تشغيلية لحالة المقالات وحركة سير العمل. لا يتضمن التقرير بيانات شخصية أو أسباب التعديلات.</p></div><label className="cms-report-range">الفترة<select value={days} onChange={(event) => setDays(Number(event.target.value))}><option value={7}>آخر 7 أيام</option><option value={30}>آخر 30 يومًا</option><option value={90}>آخر 90 يومًا</option></select></label></section>
    {error && <div className="cms-msg error" role="alert">{error}</div>}
    {!allowed && !loading && !error && <div className="cms-msg error" role="alert">هذا التقرير متاح لأدوار التحرير القيادية فقط.</div>}
    {loading && <div className="cms-loading" role="status">جارٍ تحميل التقرير…</div>}
    {report && <>
      <section className="cms-report-kpis" aria-label="مؤشرات غرفة الأخبار">
        {[ ["إجمالي المقالات", report.totalArticles], [`أُنشئت خلال ${days} يومًا`, report.createdInRange], [`نُشرت خلال ${days} يومًا`, report.publishedInRange], [`حُدثت خلال ${days} يومًا`, report.updatedInRange] ].map(([label, value]) => <article className="cms-card cms-report-kpi" key={label}><span>{label}</span><strong>{Number(value).toLocaleString("ar-EG")}</strong></article>)}
      </section>
      <section className="cms-card cms-report-section"><div className="cms-section-title"><div><h2>المقالات حسب الحالة</h2><p>الأعداد الحالية، بحسب حالة سير العمل.</p></div></div><div className="cms-report-statuses">{report.statusCounts.map((item) => <div className="cms-report-status" key={item.status}><span>{STATUS[item.status] ?? item.status}</span><strong>{item.count.toLocaleString("ar-EG")}</strong></div>)}</div></section>
      <section className="cms-card cms-report-section"><div className="cms-section-title"><div><h2>آخر انتقالات سير العمل</h2><p>آخر 30 انتقالًا مسجلًا للمقالات.</p></div><button className="cms-btn" type="button" onClick={() => void load()} disabled={loading}>تحديث</button></div>{report.recentActivity.length === 0 ? <div className="cms-empty-state">لا توجد انتقالات مسجلة حتى الآن.</div> : <div className="cms-report-table-wrap"><table className="cms-report-table"><thead><tr><th>المقال</th><th>الحالة السابقة</th><th>الحالة الجديدة</th><th>الدور المسجل</th><th>التوقيت</th></tr></thead><tbody>{report.recentActivity.map((item, index) => <tr key={`${item.occurredAtUtc}-${index}`}><td>{item.title}</td><td>{STATUS[item.fromStatus] ?? item.fromStatus}</td><td><strong>{STATUS[item.toStatus] ?? item.toStatus}</strong></td><td>{item.actorRoles.split(",").filter(Boolean).map((role) => ROLE[role] ?? role).join("، ") || "—"}</td><td>{new Intl.DateTimeFormat("ar-EG", { dateStyle: "medium", timeStyle: "short" }).format(new Date(item.occurredAtUtc))}</td></tr>)}</tbody></table></div>}</section>
    </>}
  </div>;
}
