"use client";

import { useCallback, useEffect, useState } from "react";
import { cms, type CmsSession } from "@/lib/cms-client";

type City = { id: string; countryId: string; nameAr: string; nameEn: string; slug: string; latitude: number | null; longitude: number | null };
type Country = { id: string; nameAr: string; nameEn: string; isoCode2: string; slug: string; cityCount: number; cities: City[] };
type Notice = { kind: "error" | "ok"; text: string };

async function call<T>(path = "", init?: RequestInit): Promise<T> {
  const response = await fetch(`/cms-api/geography${path}`, { ...init, credentials: "same-origin", cache: "no-store", headers: { "Content-Type": "application/json", ...init?.headers } });
  if (!response.ok) {
    const payload = await response.json().catch(() => ({})) as { message?: string; errors?: { message: string }[] };
    throw new Error(payload.errors?.map((item) => item.message).join(" · ") || payload.message || `تعذّر إتمام الطلب (${response.status}).`);
  }
  return response.json() as Promise<T>;
}

export default function CmsGeographyPage() {
  const [session, setSession] = useState<CmsSession | null>(null);
  const [countries, setCountries] = useState<Country[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [countryForm, setCountryForm] = useState({ nameAr: "", nameEn: "", isoCode2: "" });
  const [cityForm, setCityForm] = useState({ countryId: "", nameAr: "", nameEn: "", slug: "", latitude: "", longitude: "" });
  const load = useCallback(async () => {
    setLoading(true);
    try { const [current, list] = await Promise.all([cms.session(), call<Country[]>()]); setSession(current); setCountries(list); }
    catch (error) { setNotice({ kind: "error", text: error instanceof Error ? error.message : "تعذّر تحميل بيانات الدول." }); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { void load(); }, [load]);

  const canManage = !!session?.roles?.includes("EditorInChief");
  async function createCountry(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault(); setSaving(true); setNotice(null);
    try { await call("/countries", { method: "POST", body: JSON.stringify(countryForm) }); setCountryForm({ nameAr: "", nameEn: "", isoCode2: "" }); setNotice({ kind: "ok", text: "أُضيفت الدولة إلى دليل المواقع." }); await load(); }
    catch (error) { setNotice({ kind: "error", text: error instanceof Error ? error.message : "تعذّر إضافة الدولة." }); }
    finally { setSaving(false); }
  }
  async function createCity(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault(); setSaving(true); setNotice(null);
    try {
      await call("/cities", { method: "POST", body: JSON.stringify({ ...cityForm, latitude: cityForm.latitude ? Number(cityForm.latitude) : null, longitude: cityForm.longitude ? Number(cityForm.longitude) : null }) });
      setCityForm((current) => ({ ...current, nameAr: "", nameEn: "", slug: "", latitude: "", longitude: "" })); setNotice({ kind: "ok", text: "أُضيفت المدينة إلى الدليل." }); await load();
    } catch (error) { setNotice({ kind: "error", text: error instanceof Error ? error.message : "تعذّر إضافة المدينة." }); }
    finally { setSaving(false); }
  }

  return <div className="cms-page-stack">
    <section className="cms-page-heading"><div><span className="cms-eyebrow">بيانات المكان المعتمدة</span><h1>دليل الدول والمدن</h1><p>إدارة قائمة المواقع المستخدمة في الأخبار، وضع الأزمة، وخدمات «اطمّن» والخريطة.</p></div><button className="cms-btn" type="button" disabled={loading} onClick={() => void load()}>تحديث</button></section>
    {notice && <div className={`cms-msg ${notice.kind}`} role={notice.kind === "error" ? "alert" : "status"}>{notice.text}</div>}
    {!canManage && !loading && <div className="cms-msg error" role="alert">إدارة دليل الدول متاحة لرئيس التحرير فقط.</div>}
    <section className="cms-card"><div className="cms-section-title"><div><h2>إضافة دولة</h2><p className="cms-hint">استخدم رمز ISO 3166-1 alpha-2 المعتمد، مثل CA أو AE.</p></div></div><form className="cms-geo-form" onSubmit={(event) => void createCountry(event)}>
      <label className="cms-field"><span>اسم الدولة بالعربية</span><input required maxLength={100} value={countryForm.nameAr} onChange={(event) => setCountryForm({ ...countryForm, nameAr: event.target.value })} disabled={!canManage} /></label>
      <label className="cms-field"><span>اسم الدولة بالإنجليزية</span><input required maxLength={100} dir="ltr" value={countryForm.nameEn} onChange={(event) => setCountryForm({ ...countryForm, nameEn: event.target.value })} disabled={!canManage} /></label>
      <label className="cms-field"><span>رمز الدولة</span><input required minLength={2} maxLength={2} dir="ltr" value={countryForm.isoCode2} onChange={(event) => setCountryForm({ ...countryForm, isoCode2: event.target.value.toUpperCase() })} disabled={!canManage} /></label>
      <button className="cms-btn primary" disabled={!canManage || saving}>{saving ? "جارٍ الحفظ…" : "إضافة الدولة"}</button>
    </form></section>
    <section className="cms-card"><div className="cms-section-title"><div><h2>إضافة مدينة</h2><p className="cms-hint">الإحداثيات اختيارية، لكنها مطلوبة لإظهار مواقع المدن على الخريطة بدقة.</p></div></div><form className="cms-geo-form cms-geo-form--city" onSubmit={(event) => void createCity(event)}>
      <label className="cms-field"><span>الدولة</span><select required value={cityForm.countryId} onChange={(event) => setCityForm({ ...cityForm, countryId: event.target.value })} disabled={!canManage}><option value="">اختر الدولة</option>{countries.map((country) => <option key={country.id} value={country.id}>{country.nameAr}</option>)}</select></label>
      <label className="cms-field"><span>اسم المدينة بالعربية</span><input required maxLength={100} value={cityForm.nameAr} onChange={(event) => setCityForm({ ...cityForm, nameAr: event.target.value })} disabled={!canManage} /></label>
      <label className="cms-field"><span>اسم المدينة بالإنجليزية</span><input required maxLength={100} dir="ltr" value={cityForm.nameEn} onChange={(event) => setCityForm({ ...cityForm, nameEn: event.target.value })} disabled={!canManage} /></label>
      <label className="cms-field"><span>المعرّف في الرابط</span><input required pattern="[a-z0-9]+(?:-[a-z0-9]+)*" maxLength={100} dir="ltr" value={cityForm.slug} onChange={(event) => setCityForm({ ...cityForm, slug: event.target.value.toLowerCase() })} placeholder="new-york" disabled={!canManage} /></label>
      <label className="cms-field"><span>خط العرض</span><input type="number" min={-90} max={90} step="any" dir="ltr" value={cityForm.latitude} onChange={(event) => setCityForm({ ...cityForm, latitude: event.target.value })} disabled={!canManage} /></label>
      <label className="cms-field"><span>خط الطول</span><input type="number" min={-180} max={180} step="any" dir="ltr" value={cityForm.longitude} onChange={(event) => setCityForm({ ...cityForm, longitude: event.target.value })} disabled={!canManage} /></label>
      <button className="cms-btn primary" disabled={!canManage || saving || countries.length === 0}>{saving ? "جارٍ الحفظ…" : "إضافة المدينة"}</button>
    </form></section>
    <section className="cms-card"><div className="cms-section-title"><div><h2>الدليل الحالي</h2><p className="cms-hint">لا تظهر البيانات إلا بعد إضافتها واعتمادها هنا.</p></div><span className="cms-count-pill">{countries.length} دولة</span></div>
      {loading ? <p>جارٍ تحميل الدليل…</p> : countries.length ? <div className="cms-geo-list">{countries.map((country) => <details key={country.id}><summary><span><strong>{country.nameAr}</strong><small dir="ltr">{country.nameEn} · {country.isoCode2}</small></span><span>{country.cityCount} مدينة</span></summary>{country.cities.length ? <ul>{country.cities.map((city) => <li key={city.id}><span>{city.nameAr}<small dir="ltr">{city.nameEn} · {city.slug}</small></span>{city.latitude !== null && city.longitude !== null && <code dir="ltr">{city.latitude}, {city.longitude}</code>}</li>)}</ul> : <p className="cms-hint">لا توجد مدن مسجلة لهذه الدولة بعد.</p>}</details>)}</div> : <div className="cms-news-empty">الدليل فارغ. أضف الدول والمدن التي يغطيها الموقع من النموذجين أعلاه.</div>}
    </section>
  </div>;
}
