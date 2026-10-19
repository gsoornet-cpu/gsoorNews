"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { CmsError, cms } from "@/lib/cms-client";
import { formatArabicDate } from "@/lib/format";

type CaseStatus = string | number;
type CaseSummary = { caseId: string; status: CaseStatus; subjectName: string; createdAtUtc: string; assignedAtUtc: string | null; countryNameAr: string | null; cityNameAr: string | null };
type CaseDetail = CaseSummary & { subjectContactPhone: string | null; concernDescription: string; applicantPhoneE164: string; otpVerifiedAtUtc: string };
type CasesPage = { items: CaseSummary[]; page: number; pageSize: number; totalCount: number; totalPages: number };
const STATUS: Record<string, string> = { "1": "لا معلومة موثقة بعد", "2": "جارٍ التحقق", "3": "محاولة تواصل جارية", "4": "تواصل تم تأكيده", "5": "الهوية مؤكدة", "6": "تم إبلاغ الأسرة", "7": "معلومة رسمية متاحة", "8": "تعذر التحقق", NoVerifiedInformationYet: "لا معلومة موثقة بعد", VerificationInProgress: "جارٍ التحقق", ContactAttemptInProgress: "محاولة تواصل جارية", ContactConfirmed: "تواصل تم تأكيده", IdentityConfirmed: "الهوية مؤكدة", FamilyInformed: "تم إبلاغ الأسرة", OfficialInformationAvailable: "معلومة رسمية متاحة", VerificationFailed: "تعذر التحقق" };

export default function CmsAtmaenPage() {
  const router = useRouter();
  const [data, setData] = useState<CasesPage | null>(null);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [selectedCase, setSelectedCase] = useState<CaseDetail | null>(null);
  const [selectedStatus, setSelectedStatus] = useState("");
  const [detailLoading, setDetailLoading] = useState(false);
  const [savingStatus, setSavingStatus] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const load = useCallback(async () => {
    setLoading(true); setError(null);
    try {
      const session = await cms.session();
      if (!session.authenticated) { router.replace("/cms/login"); return; }
      if (!session.roles?.includes("CrisisEditor")) { setError("قائمة الحالات متاحة لمحرري اطمّن المخولين فقط."); return; }
      const response = await fetch(`/cms-api/atmaen/cases/my-queue?page=${page}&pageSize=20`, { credentials: "same-origin", cache: "no-store" });
      if (!response.ok) {
        const result = await response.json().catch(() => ({})) as { message?: string };
        throw new CmsError(result.message ?? (response.status === 403 ? "ليست لديك صلاحية الوصول لهذه القائمة." : "تعذر تحميل الحالات."), response.status);
      }
      setData(await response.json() as CasesPage);
    } catch (e) {
      if (e instanceof CmsError && e.status === 401) router.replace("/cms/login");
      else setError(e instanceof Error ? e.message : "تعذر تحميل الحالات.");
    } finally { setLoading(false); }
  }, [page, router]);
  useEffect(() => { void load(); }, [load]);
  async function openCase(item: CaseSummary) {
    setSelectedCase(null); setSelectedStatus(String(item.status)); setDetailLoading(true); setError(null);
    try {
      const response = await fetch(`/cms-api/atmaen/cases/${encodeURIComponent(item.caseId)}`, { credentials: "same-origin", cache: "no-store" });
      if (!response.ok) { const result = await response.json().catch(() => ({})) as { message?: string }; throw new Error(result.message ?? "تعذر تحميل تفاصيل الحالة."); }
      const detail = await response.json() as CaseDetail;
      setSelectedCase(detail); setSelectedStatus(String(detail.status));
    } catch (e) { setError(e instanceof Error ? e.message : "تعذر تحميل تفاصيل الحالة."); }
    finally { setDetailLoading(false); }
  }
  async function saveStatus() {
    if (!selectedCase) return;
    setSavingStatus(true); setError(null); setNotice(null);
    try {
      const response = await fetch(`/cms-api/atmaen/cases/${encodeURIComponent(selectedCase.caseId)}/status`, { method: "POST", credentials: "same-origin", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ newStatus: Number(selectedStatus) }) });
      if (!response.ok) { const result = await response.json().catch(() => ({})) as { message?: string }; throw new Error(result.message ?? "تعذر تحديث الحالة."); }
      setSelectedCase((current) => current ? { ...current, status: Number(selectedStatus) } : current);
      setNotice("تم حفظ حالة المتابعة.");
      await load();
    } catch (e) { setError(e instanceof Error ? e.message : "تعذر تحديث الحالة."); }
    finally { setSavingStatus(false); }
  }
  const items = data?.items ?? [];
  return <div className="cms-page-stack">
    <section className="cms-page-heading cms-atmaen-heading"><div><span className="cms-eyebrow">إدارة الحالات المصرح بها</span><h1>اطمّن</h1><p>قائمة الحالات المسندة إليك مرتبة بالأقدم أولًا لمتابعة عادلة.</p></div><span className="cms-heading-icon" aria-hidden="true">♥</span></section>
    <div className="cms-stat-grid"><article><span>إجمالي الحالات المسندة</span><strong>{data?.totalCount ?? "—"}</strong></article><article><span>في هذه الصفحة</span><strong>{items.length}</strong></article><article><span>صفحة العرض</span><strong>{data ? `${data.page} / ${data.totalPages}` : "—"}</strong></article></div>
    <section className="cms-card"><div className="cms-section-title"><div><h2>قائمة المتابعة</h2><p className="cms-hint">تعرض القائمة الحد الأدنى من البيانات اللازمة للفرز. افتح الحالة للوصول إلى التفاصيل بصلاحية محرر الأزمات المسندة إليه.</p></div><button className="cms-btn" type="button" disabled={loading} onClick={() => void load()}>{loading ? "جارٍ التحديث…" : "تحديث"}</button></div>
      {error && <div className="cms-msg error" role="alert">{error}</div>}
      {loading && !data ? <p>جارٍ تحميل الحالات…</p> : items.length ? <div className="cms-table-scroll"><table className="cms-table"><thead><tr><th>الحالة</th><th>المرحلة</th><th>الموقع</th><th>تاريخ الوصول</th><th>تاريخ الإسناد</th><th>التفاصيل</th></tr></thead><tbody>{items.map((item) => <tr key={item.caseId}><td data-label="الحالة"><strong>{item.subjectName}</strong></td><td data-label="المرحلة"><span className="cms-badge review">{STATUS[String(item.status)] ?? String(item.status)}</span></td><td data-label="الموقع">{[item.cityNameAr, item.countryNameAr].filter(Boolean).join("، ") || "غير محدد"}</td><td data-label="تاريخ الوصول">{formatArabicDate(item.createdAtUtc)}</td><td data-label="تاريخ الإسناد">{item.assignedAtUtc ? formatArabicDate(item.assignedAtUtc) : "—"}</td><td data-label="التفاصيل"><button className="cms-btn" type="button" onClick={() => void openCase(item)}>فتح الحالة</button></td></tr>)}</tbody></table></div> : !error && <div className="cms-news-empty">لا توجد حالات مسندة إليك حاليًا.</div>}
      {data && data.totalPages > 1 && <div className="cms-pager"><button className="cms-btn" disabled={page <= 1 || loading} onClick={() => setPage((value) => Math.max(1, value - 1))}>السابق</button><span>{data.page} من {data.totalPages}</span><button className="cms-btn" disabled={page >= data.totalPages || loading} onClick={() => setPage((value) => Math.min(data.totalPages, value + 1))}>التالي</button></div>}
    </section>
    <p className="cms-hint cms-security-note">بيانات اطمّن سرية. لا تظهر أرقام التواصل في قائمة العرض، وتبقى صلاحية الحالة محصورة في محرر الأزمات المسندة إليه.</p>
    {detailLoading && <div className="cms-dialog-backdrop"><div className="cms-card" role="status">جارٍ تحميل تفاصيل الحالة…</div></div>}
    {selectedCase && <div className="cms-dialog-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) setSelectedCase(null); }}><section className="cms-card cms-case-dialog" role="dialog" aria-modal="true" aria-labelledby="cms-case-title"><div className="cms-section-title"><div><span className="cms-eyebrow">ملف حالة سري</span><h2 id="cms-case-title">{selectedCase.subjectName}</h2><p className="cms-hint">{[selectedCase.cityNameAr, selectedCase.countryNameAr].filter(Boolean).join("، ") || "الموقع غير محدد"}</p></div><button className="cms-btn" type="button" onClick={() => setSelectedCase(null)} aria-label="إغلاق تفاصيل الحالة">إغلاق</button></div><div className="cms-case-details"><article><span>وصف الحالة</span><p>{selectedCase.concernDescription || "لم تتم إضافة وصف."}</p></article><article><span>رقم التواصل مع صاحب البلاغ</span><b dir="ltr">{selectedCase.applicantPhoneE164}</b></article><article><span>رقم التواصل الخاص بالحالة</span><b dir="ltr">{selectedCase.subjectContactPhone || "غير متاح"}</b></article><article><span>التحقق عبر OTP</span><b>{formatArabicDate(selectedCase.otpVerifiedAtUtc)}</b></article><article><span>تاريخ إنشاء الحالة</span><b>{formatArabicDate(selectedCase.createdAtUtc)}</b></article></div><div className="cms-case-status-form"><label htmlFor="atmaen-status">حالة المتابعة</label><select id="atmaen-status" value={selectedStatus} onChange={(event) => setSelectedStatus(event.target.value)}>{Object.entries(STATUS).filter(([key]) => /^\d+$/.test(key)).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select><button className="cms-btn primary" type="button" disabled={savingStatus || selectedStatus === String(selectedCase.status)} onClick={() => void saveStatus()}>{savingStatus ? "جارٍ الحفظ…" : "حفظ الحالة"}</button></div>{notice && <div className="cms-msg ok" role="status">{notice}</div>}</section></div>}
  </div>;
}
