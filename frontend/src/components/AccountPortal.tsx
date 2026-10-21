"use client";

import type { FormEvent } from "react";
import { useEffect, useState } from "react";

type Place = { cityId: string; cityNameAr: string; countryNameAr: string | null };
type Session = { authenticated: true; profile: { privacyLevel: string; countryNameAr: string | null; regionNameAr: string | null; cityNameAr: string | null; places: Place[] } };
type Country = { id: string; nameAr: string; nameEn: string; isoCode2: string };
type City = { id: string; countryId: string; nameAr: string; nameEn: string };
const PRIVACY_LABELS: Record<string, string> = { NoLocation: "بدون موقع", CountryOnly: "الدولة فقط", Region: "المنطقة", City: "المدينة" };

export function AccountPortal() {
  const [session, setSession] = useState<Session | null>(null);
  const [mode, setMode] = useState<"login" | "register">("login");
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState("");
  const [countries, setCountries] = useState<Country[]>([]);
  const [cities, setCities] = useState<City[]>([]);
  const [countryId, setCountryId] = useState("");
  const [cityId, setCityId] = useState("");
  const [privacyLevel, setPrivacyLevel] = useState("NoLocation");

  async function refreshSession() {
    const response = await fetch("/account-api/session", { cache: "no-store" }).catch(() => null);
    if (!response?.ok) { setSession(null); return; }
    const current = await response.json() as Session;
    setSession(current);
    setPrivacyLevel(current.profile.privacyLevel);
    const catalog = await fetch("/account-api/geography", { cache: "no-store" }).then((r) => r.ok ? r.json() as Promise<Country[]> : []).catch(() => []);
    setCountries(catalog);
    const currentCountry = catalog.find((country) => country.nameAr === current.profile.countryNameAr);
    setCountryId(currentCountry?.id ?? "");
  }

  useEffect(() => {
    void refreshSession().finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (!countryId) { setCities([]); setCityId(""); return; }
    let active = true;
    fetch(`/account-api/geography?countryId=${encodeURIComponent(countryId)}`, { cache: "no-store" })
      .then(async (response) => response.ok ? response.json() as Promise<City[]> : [])
      .then((items) => { if (active) setCities(items); })
      .catch(() => { if (active) setCities([]); });
    return () => { active = false; };
  }, [countryId]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setMessage("");
    const data = new FormData(event.currentTarget);
    const response = await fetch("/account-api/session", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ action: mode, email: data.get("email"), password: data.get("password"), displayName: data.get("displayName") }) }).catch(() => null);
    const result = await response?.json().catch(() => ({})) as { message?: string } | undefined;
    if (response?.ok) await refreshSession();
    else setMessage(result?.message ?? "تعذّر الاتصال بالخادم. حاول مجددًا.");
    setBusy(false);
  }

  async function saveLocation() {
    setBusy(true); setMessage("");
    const privacyCode = privacyLevel === "CountryOnly" ? 1 : privacyLevel === "City" ? 3 : 0;
    const payload = { privacyLevel: privacyCode, countryId: privacyLevel === "NoLocation" ? null : countryId || null, regionId: null, cityId: privacyLevel === "City" ? cityId || null : null };
    const response = await fetch("/account-api/profile", { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload) }).catch(() => null);
    const result = await response?.json().catch(() => ({})) as { message?: string } | undefined;
    if (response?.ok) { setMessage("تم حفظ إعدادات الموقع."); await refreshSession(); }
    else setMessage(result?.message ?? "تعذّر حفظ إعدادات الموقع.");
    setBusy(false);
  }

  async function followCity() {
    if (!cityId) return;
    setBusy(true); setMessage("");
    const response = await fetch("/account-api/profile/places", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ cityId }) }).catch(() => null);
    if (response?.ok) { setMessage("أضيفت المدينة إلى قائمة المتابعة."); await refreshSession(); }
    else { const error = await response?.json().catch(() => ({})) as { message?: string } | undefined; setMessage(error?.message ?? "تعذّر إضافة المدينة."); }
    setBusy(false);
  }

  async function removePlace(place: Place) {
    setBusy(true); setMessage("");
    const response = await fetch(`/account-api/profile/places?cityId=${encodeURIComponent(place.cityId)}`, { method: "DELETE" }).catch(() => null);
    if (response?.ok) { setMessage("أزيلت المدينة من قائمة المتابعة."); await refreshSession(); }
    else setMessage("تعذّر إزالة المدينة.");
    setBusy(false);
  }

  async function logout() {
    await fetch("/account-api/session", { method: "DELETE" }); setSession(null); setMessage("تم تسجيل الخروج.");
  }

  if (loading) return <section className="account-card" aria-busy="true"><p>جارٍ تحميل الحساب…</p></section>;
  if (session) return <section className="account-card" aria-live="polite"><span className="eyebrow">ملف المهجر</span><h1>مرحبًا بك في جسور</h1>
    <p>مستوى مشاركة الموقع: <strong>{PRIVACY_LABELS[session.profile.privacyLevel] ?? "غير محدد"}</strong></p>
    <p>الموقع المسجل: {[session.profile.cityNameAr, session.profile.regionNameAr, session.profile.countryNameAr].filter(Boolean).join("، ") || "لم تضف موقعًا"}</p>
    <h2>إعدادات الموقع</h2><label>مستوى مشاركة الموقع<select value={privacyLevel} onChange={(event) => setPrivacyLevel(event.target.value)}><option value="NoLocation">بدون موقع</option><option value="CountryOnly">الدولة فقط</option><option value="City">المدينة</option></select></label>
    {privacyLevel !== "NoLocation" && <label>الدولة<select value={countryId} onChange={(event) => { setCountryId(event.target.value); setCityId(""); }}><option value="">اختر الدولة</option>{countries.map((country) => <option key={country.id} value={country.id}>{country.nameAr}</option>)}</select></label>}
    {privacyLevel === "City" && <label>المدينة<select value={cityId} onChange={(event) => setCityId(event.target.value)}><option value="">اختر المدينة</option>{cities.map((city) => <option key={city.id} value={city.id}>{city.nameAr}</option>)}</select></label>}
    <button type="button" className="btn btn-primary" onClick={saveLocation} disabled={busy || (privacyLevel !== "NoLocation" && !countryId) || (privacyLevel === "City" && !cityId)}>حفظ الموقع</button>
    <h2>مدن تتابعها</h2><div className="account-follow-row"><select aria-label="اختر مدينة للمتابعة" value={cityId} onChange={(event) => setCityId(event.target.value)}><option value="">اختر مدينة</option>{cities.map((city) => <option key={city.id} value={city.id}>{city.nameAr}</option>)}</select><button type="button" className="btn btn-primary" onClick={followCity} disabled={busy || !cityId}>إضافة</button></div>
    {session.profile.places.length ? <ul className="account-places">{session.profile.places.map((place) => <li key={place.cityId}><span>{place.cityNameAr}{place.countryNameAr ? `، ${place.countryNameAr}` : ""}</span><button type="button" onClick={() => removePlace(place)} disabled={busy} aria-label={`إزالة ${place.cityNameAr}`}>إزالة</button></li>)}</ul> : <p>لا تتابع مدنًا بعد.</p>}
    {message && <p className="account-message" role="status">{message}</p>}<p className="account-note">يمكنك اختيار «بدون موقع» في أي وقت. لا يعرض الملف موقعك الدقيق للجمهور.</p><button type="button" className="btn btn-outline" onClick={logout}>تسجيل الخروج</button>
  </section>;

  return <section className="account-card"><span className="eyebrow">حساب جسور</span><h1>{mode === "login" ? "تسجيل الدخول" : "إنشاء حساب"}</h1><p>استخدم حسابك للوصول إلى خدمات الملف الشخصي المتاحة.</p><form onSubmit={submit}>
    {mode === "register" && <label>الاسم الظاهر<input name="displayName" autoComplete="name" required maxLength={120}/></label>}
    <label>البريد الإلكتروني<input name="email" type="email" autoComplete="email" required maxLength={256}/></label>
    <label>كلمة المرور<input name="password" type="password" autoComplete={mode === "login" ? "current-password" : "new-password"} required minLength={8} maxLength={128}/></label>
    {message && <p className="account-message" role="alert">{message}</p>}
    <button className="btn btn-primary" type="submit" disabled={busy}>{busy ? "جارٍ التنفيذ…" : mode === "login" ? "دخول" : "إنشاء الحساب"}</button>
  </form><button className="account-switch" type="button" onClick={() => { setMode(mode === "login" ? "register" : "login"); setMessage(""); }}>{mode === "login" ? "مستخدم جديد؟ أنشئ حسابًا" : "لديك حساب؟ سجّل الدخول"}</button><p className="account-note">لا تتوفر حاليًا خدمة استعادة كلمة المرور أو التحقق عبر الرسائل النصية في نسخة التجربة.</p></section>;
}
