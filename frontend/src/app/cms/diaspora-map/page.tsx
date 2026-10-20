"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { cms, type CmsSession } from "@/lib/cms-client";

type City = { id: string; countryId: string; nameAr: string; nameEn: string; slug: string; latitude: number | null; longitude: number | null };
type Country = { id: string; nameAr: string; nameEn: string; isoCode2: string; cities: City[] };
type Estimate = { id: string; countryId: string; countryNameAr: string; cityId: string | null; cityNameAr: string | null; estimatedPopulation: number; sourceName: string; sourceUrl: string; sourcePublishedOn: string; isPublished: boolean };
type FormState = { countryId: string; cityId: string; estimatedPopulation: string; sourceName: string; sourceUrl: string; sourcePublishedOn: string; isPublished: boolean };
const EMPTY: FormState = { countryId: "", cityId: "", estimatedPopulation: "", sourceName: "", sourceUrl: "", sourcePublishedOn: "", isPublished: false };

async function call<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/cms-api/diaspora-estimates${path}`, { ...init, credentials: "same-origin", cache: "no-store", headers: { "Content-Type": "application/json", ...init?.headers } });
  if (!response.ok) {
    const body = await response.json().catch(() => ({})) as { message?: string; errors?: { message: string }[] };
    throw new Error(body.errors?.map((item) => item.message).join(" · ") || body.message || `تعذّر إتمام الطلب (${response.status}).`);
  }
  return response.status === 204 ? undefined as T : response.json() as Promise<T>;
}

export default function CmsDiasporaMapPage() {
  const [session, setSession] = useState<CmsSession | null>(null);
  const [catalog, setCatalog] = useState<Country[]>([]);
  const [items, setItems] = useState<Estimate[]>([]);
  const [form, setForm] = useState(EMPTY);
  const [editing, setEditing] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const canManage = !!session?.roles?.includes("EditorInChief");
  const cities = useMemo(() => catalog.find((country) => country.id === form.countryId)?.cities ?? [], [catalog, form.countryId]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [current, countries, estimates] = await Promise.all([
        cms.session(),
        fetch("/cms-api/geography", { credentials: "same-origin", cache: "no-store" }).then(async (response) => {
          if (!response.ok) throw new Error("تعذّر تحميل دليل المواقع.");
          return response.json() as Promise<Country[]>;
        }),
        call<Estimate[]>(""),
      ]);
      setSession(current); setCatalog(countries); setItems(estimates);
    } catch (reason) { setError(reason instanceof Error ? reason.message : "تعذّر تحميل بيانات الخريطة."); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { void load(); }, [load]);

  function reset() { setForm(EMPTY); setEditing(null); setError(""); }
  function startEdit(item: Estimate) {
    setEditing(item.id);
    setForm({ countryId: item.countryId, cityId: item.cityId ?? "", estimatedPopulation: String(item.estimatedPopulation), sourceName: item.sourceName, sourceUrl: item.sourceUrl, sourcePublishedOn: item.sourcePublishedOn, isPublished: item.isPublished });
    setError(""); setNotice("");
    window.scrollTo({ top: 0, behavior: "smooth" });
  }
  async function save(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError(""); setNotice("");
    const payload = { ...form, cityId: form.cityId || null, estimatedPopulation: Number(form.estimatedPopulation) };
    try {
      await call(editing ? `/${editing}` : "", { method: editing ? "PUT" : "POST", body: JSON.stringify(payload) });
      setNotice(editing ? "تم تحديث التقدير." : "تمت إضافة التقدير."); reset(); await load();
    } catch (reason) { setError(reason instanceof Error ? reason.message : "تعذّر حفظ التقدير."); }
    finally { setBusy(false); }
  }
  async function remove(item: Estimate) {
    if (!window.confirm(`حذف تقدير ${item.cityNameAr ?? item.countryNameAr}؟`)) return;
    setBusy(true); setError(""); setNotice("");
    try { await call(`/${item.id}`, { method: "DELETE" }); setItems((current) => current.filter((row) => row.id !== item.id)); setNotice("تم حذف التقدير."); }
    catch (reason) { setError(reason instanceof Error ? reason.message : "تعذّر حذف التقدير."); }
    finally { setBusy(false); }
  }

  return <div className="cms-page-stack">
    <section className="cms-page-heading"><div><span className="cms-eyebrow">بيانات جاليات موثقة</span><h1>خريطة المهجر</h1><p>أضف تقديرات منشورة مع مصدرها وتاريخها. لا تظهر التقديرات للعامة قبل اعتمادها.</p></div><button className="cms-btn" type="button" disabled={loading || busy} onClick={() => void load()}>تحديث</button></section>
    <div className="cms-map-privacy-note"><strong>الخصوصية والمصدر</strong><p>تُعرض الأرقام التقديرية منفصلة عن مؤشرات مستخدمي جسور. المؤشر المجتمعي لا يظهر إلا عند بلوغ 25 مشاركة موقع على الأقل، ولا تُعرض مواقع الأفراد. الأرقام المنشورة هنا تقديرية وليست إحصاءً حكوميًا.</p></div>
    {error && <div className="cms-msg error" role="alert">{error}</div>}{notice && <div className="cms-msg ok" role="status">{notice}</div>}
    {!canManage && !loading && <div className="cms-msg error" role="alert">إدارة بيانات الخريطة متاحة لرئيس التحرير فقط.</div>}
    <section className="cms-card cms-map-editor"><div className="cms-section-title"><div><h2>{editing ? "تعديل تقدير" : "إضافة تقدير موثق"}</h2><p className="cms-hint">أدخل الرقم كما ورد في المصدر. لا تضف تقديرًا بلا رابط يمكن التحقق منه.</p></div>{editing && <button className="cms-btn" type="button" onClick={reset}>إلغاء التعديل</button>}</div>
      <form className="cms-map-form" onSubmit={(event) => void save(event)}>
        <label className="cms-field"><span>الدولة</span><select required value={form.countryId} onChange={(event) => setForm({ ...form, countryId: event.target.value, cityId: "" })} disabled={!canManage || busy}><option value="">اختر الدولة</option>{catalog.map((country) => <option key={country.id} value={country.id}>{country.nameAr}</option>)}</select></label>
        <label className="cms-field"><span>الموقع</span><select value={form.cityId} onChange={(event) => setForm({ ...form, cityId: event.target.value })} disabled={!canManage || busy || !form.countryId}><option value="">الدولة بالكامل</option>{cities.map((city) => <option key={city.id} value={city.id}>{city.nameAr}{city.latitude !== null && city.longitude !== null ? " · محدد على الخريطة" : ""}</option>)}</select></label>
        <label className="cms-field"><span>عدد المصريين التقديري</span><input type="number" required min={1} max={1000000000} step={1} value={form.estimatedPopulation} onChange={(event) => setForm({ ...form, estimatedPopulation: event.target.value })} disabled={!canManage || busy} /></label>
        <label className="cms-field"><span>اسم المصدر</span><input required maxLength={200} value={form.sourceName} onChange={(event) => setForm({ ...form, sourceName: event.target.value })} disabled={!canManage || busy} placeholder="مثال: تقرير منشور…" /></label>
        <label className="cms-field cms-map-source-url"><span>رابط المصدر</span><input required type="url" maxLength={2048} dir="ltr" value={form.sourceUrl} onChange={(event) => setForm({ ...form, sourceUrl: event.target.value })} disabled={!canManage || busy} placeholder="https://" /></label>
        <label className="cms-field"><span>تاريخ نشر المصدر</span><input required type="date" max={new Date().toISOString().slice(0, 10)} value={form.sourcePublishedOn} onChange={(event) => setForm({ ...form, sourcePublishedOn: event.target.value })} disabled={!canManage || busy} /></label>
        <label className="cms-map-publish"><input type="checkbox" checked={form.isPublished} onChange={(event) => setForm({ ...form, isPublished: event.target.checked })} disabled={!canManage || busy} /><span>إظهار هذا التقدير على الخريطة العامة</span></label>
        <div className="cms-actions"><button className="cms-btn primary" disabled={!canManage || busy || catalog.length === 0}>{busy ? "جارٍ الحفظ…" : editing ? "حفظ التعديلات" : "إضافة التقدير"}</button></div>
      </form>
      {catalog.length === 0 && <p className="cms-hint">أضف الدول والمدن أولًا من صفحة دليل الدول والمدن.</p>}
    </section>
    <section className="cms-card"><div className="cms-section-title"><div><h2>التقديرات المسجلة</h2><p className="cms-hint">المسودات لا تظهر في الموقع، وكل رقم منشور يحمل مصدره.</p></div><span className="cms-count-pill">{items.length} موقع</span></div>
      {loading ? <p>جارٍ تحميل البيانات…</p> : items.length ? <div className="cms-table-scroll"><table className="cms-table"><thead><tr><th>الموقع</th><th>التقدير</th><th>المصدر وتاريخه</th><th>الحالة</th><th>إجراءات</th></tr></thead><tbody>{items.map((item) => <tr key={item.id}><td data-label="الموقع"><strong>{item.cityNameAr ?? item.countryNameAr}</strong>{item.cityNameAr && <small className="cms-hint">{item.countryNameAr}</small>}</td><td data-label="التقدير">{item.estimatedPopulation.toLocaleString("ar-EG")}</td><td data-label="المصدر"><a href={item.sourceUrl} target="_blank" rel="noopener noreferrer">{item.sourceName} ↗</a><small>{item.sourcePublishedOn}</small></td><td data-label="الحالة"><span className={`cms-badge ${item.isPublished ? "published" : "draft"}`}>{item.isPublished ? "منشور" : "مسودة"}</span></td><td data-label="إجراءات"><div className="cms-actions"><button className="cms-btn" type="button" disabled={busy} onClick={() => startEdit(item)}>تعديل</button><button className="cms-btn danger" type="button" disabled={busy} onClick={() => void remove(item)}>حذف</button></div></td></tr>)}</tbody></table></div> : <div className="cms-news-empty">لا توجد تقديرات بعد. أضف أرقامًا منشورة مع مصدرها وتاريخها، أو اترك الخريطة فارغة إلى أن تتوفر بيانات موثوقة.</div>}
    </section>
  </div>;
}
