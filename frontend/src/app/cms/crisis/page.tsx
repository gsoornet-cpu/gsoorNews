"use client";

import { useCallback, useEffect, useState } from "react";
import { ApiError, apiFetch } from "@/lib/api-client";
import { cms, type CmsSession } from "@/lib/cms-client";

type Country = { id: string; nameAr: string; nameEn: string; isoCode2: string };
type CrisisConfig = { isActive: boolean; title: string; summary: string; sourceName: string; sourceUrl: string; severity: "advisory" | "urgent" | "emergency"; affectedCountryIds: string[]; lastModifiedAtUtc: string | null };
const EMPTY: CrisisConfig = { isActive: false, title: "", summary: "", sourceName: "", sourceUrl: "", severity: "urgent", affectedCountryIds: [], lastModifiedAtUtc: null };
const SEVERITY: Record<CrisisConfig["severity"], string> = { advisory: "تنبيه", urgent: "عاجل", emergency: "طوارئ" };

async function responseError(response: Response) {
  try {
    const body = await response.json() as { message?: string };
    return body.message ?? `تعذّر إتمام الطلب (${response.status}).`;
  } catch { return `تعذّر إتمام الطلب (${response.status}).`; }
}

export default function CmsCrisisModePage() {
  const [session, setSession] = useState<CmsSession | null>(null);
  const [countries, setCountries] = useState<Country[]>([]);
  const [config, setConfig] = useState<CrisisConfig>(EMPTY);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  const load = useCallback(async () => {
    setLoading(true); setError("");
    try {
      const [currentSession, countryRows, currentConfig] = await Promise.all([
        cms.session(),
        apiFetch<Country[]>("/api/v1/geography/countries", { cache: "no-store" }),
        fetch("/cms-api/crisis", { credentials: "same-origin", cache: "no-store" }).then(async (response) => {
          if (!response.ok) throw new Error(await responseError(response));
          return response.json() as Promise<CrisisConfig>;
        }),
      ]);
      setSession(currentSession); setCountries(countryRows); setConfig(currentConfig);
    } catch (cause) {
      setError(cause instanceof ApiError && cause.status === 503 ? "تعذّر تحميل قائمة الدول من الخادم." : cause instanceof Error ? cause.message : "تعذّر تحميل الإعدادات.");
    } finally { setLoading(false); }
  }, []);

  useEffect(() => { void load(); }, [load]);

  async function save(nextConfig: CrisisConfig = config) {
    if (nextConfig.isActive && nextConfig.affectedCountryIds.length === 0) { setError("اختر دولة واحدة على الأقل قبل التفعيل."); return; }
    if (!canManage) return;
    if (!config.isActive && nextConfig.isActive && !window.confirm("سيظهر تنبيه الأزمة علنًا في الموقع. تأكد من مراجعة المعلومات والدول المحددة قبل التفعيل.")) return;
    setSaving(true); setError(""); setNotice("");
    try {
      const response = await fetch("/cms-api/crisis", { method: "PUT", credentials: "same-origin", headers: { "Content-Type": "application/json" }, body: JSON.stringify(nextConfig) });
      if (!response.ok) throw new Error(await responseError(response));
      setConfig(await response.json() as CrisisConfig); setNotice("تم حفظ إعدادات وضع الأزمة.");
    } catch (cause) { setError(cause instanceof Error ? cause.message : "تعذّر حفظ الإعدادات."); }
    finally { setSaving(false); }
  }

  function toggleCountry(id: string) {
    setConfig((current) => ({ ...current, affectedCountryIds: current.affectedCountryIds.includes(id) ? current.affectedCountryIds.filter((countryId) => countryId !== id) : [...current.affectedCountryIds, id] }));
  }

  const canManage = !!session?.roles?.some((role) => role === "EditorInChief" || role === "ManagingEditor");
  if (loading) return <div className="cms-page-stack"><p className="cms-hint">جارٍ تحميل وضع الأزمة…</p></div>;

  return <div className="cms-page-stack">
    <section className="cms-page-heading cms-crisis-heading"><div><span className="cms-eyebrow">إدارة الطوارئ التحريرية</span><h1>وضع الأزمة</h1><p>فعّل تنبيهًا عامًا للدول المتأثرة. لا يُعرض في الموقع أي إحصاء للحالات الخاصة.</p></div><span className={`cms-crisis-state${config.isActive ? " is-active" : ""}`}><i />{config.isActive ? "نشط على الموقع" : "غير نشط"}</span></section>
    {!canManage && <div className="cms-msg error" role="alert">تتطلب إدارة وضع الأزمة صلاحية مدير التحرير أو رئيس التحرير.</div>}
    {error && <div className="cms-msg error" role="alert">{error}</div>}{notice && <div className="cms-msg ok" role="status">{notice}</div>}
    <section className="cms-card cms-crisis-card">
      <div className="cms-section-title"><div><h2>رسالة الأزمة العامة</h2><p className="cms-hint">استخدم معلومات موثوقة ومراجعة. تظهر الرسالة عند تفعيل الوضع فقط.</p></div></div>
      <div className="cms-crisis-form">
        <label className="cms-field"><span>عنوان التنبيه</span><input maxLength={200} value={config.title} onChange={(event) => setConfig({ ...config, title: event.target.value })} placeholder="مثال: تنبيه للمصريين في…" disabled={!canManage} /><small>{config.title.length} / 200</small></label>
        <label className="cms-field"><span>درجة التنبيه</span><select value={config.severity} onChange={(event) => setConfig({ ...config, severity: event.target.value as CrisisConfig["severity"] })} disabled={!canManage}>{Object.entries(SEVERITY).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
        <label className="cms-field cms-crisis-summary"><span>ملخص موثّق</span><textarea maxLength={2000} rows={5} value={config.summary} onChange={(event) => setConfig({ ...config, summary: event.target.value })} placeholder="اكتب ما تأكد من مصادر موثوقة، وما لا يزال قيد المتابعة." disabled={!canManage} /><small>{config.summary.length} / 2000</small></label>
        <label className="cms-field"><span>اسم المصدر</span><input maxLength={160} value={config.sourceName} onChange={(event) => setConfig({ ...config, sourceName: event.target.value })} placeholder="مثال: بيان رسمي من…" disabled={!canManage} /></label>
        <label className="cms-field"><span>رابط المصدر</span><input type="url" maxLength={2048} dir="ltr" value={config.sourceUrl} onChange={(event) => setConfig({ ...config, sourceUrl: event.target.value })} placeholder="https://" disabled={!canManage} /></label>
      </div>
      <div className="cms-crisis-country-header"><div><h3>الدول المتأثرة</h3><p className="cms-hint">يظهر التنبيه مع إشارة البلد للزوار. اختر الدول التي تشملها الرسالة فقط.</p></div><span>{config.affectedCountryIds.length} دولة</span></div>
      {countries.length ? <div className="cms-crisis-countries">{countries.map((country) => <label className={config.affectedCountryIds.includes(country.id) ? "selected" : ""} key={country.id}><input type="checkbox" checked={config.affectedCountryIds.includes(country.id)} onChange={() => toggleCountry(country.id)} disabled={!canManage} /><span>{country.nameAr}</span><small dir="ltr">{country.isoCode2}</small></label>)}</div> : <p className="cms-news-empty">لا توجد دول في قاعدة البيانات. أضف بيانات الدول أولًا قبل تفعيل وضع الأزمة.</p>}
      <div className="cms-crisis-footer"><p className="cms-hint">آخر تحديث: {config.lastModifiedAtUtc ? new Intl.DateTimeFormat("ar-EG", { dateStyle: "medium", timeStyle: "short" }).format(new Date(config.lastModifiedAtUtc)) : "لم يُحفظ بعد"}</p><div className="cms-actions"><button className="cms-btn danger" type="button" disabled={!canManage || saving || !config.isActive} onClick={() => void save({ ...config, isActive: false })}>{saving ? "جارٍ الحفظ…" : "إيقاف التنبيه"}</button><button className="cms-btn primary" type="button" disabled={!canManage || saving || countries.length === 0} onClick={() => void save({ ...config, isActive: true })}>{saving ? "جارٍ الحفظ…" : config.isActive ? "حفظ التعديلات" : "حفظ وتفعيل"}</button></div></div>
    </section>
    <aside className="cms-crisis-guidance"><strong>حدود مهمة</strong><p>هذه الرسالة لا تمثل جهة طوارئ رسمية. بيانات حالات «اطمّن» سرية ولا تدخل في ملخص الأزمة العام. اربط أي تحديث صحفي بخبر منشور ومعتمد من سير العمل التحريري.</p></aside>
  </div>;
}
