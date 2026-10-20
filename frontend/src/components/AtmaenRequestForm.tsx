"use client";

import { useEffect, useState } from "react";

type Country = { id: string; nameAr: string; nameEn: string; isoCode2: string };
type City = { id: string; countryId: string; nameAr: string; nameEn: string };
type Step = "details" | "verify" | "success";
type FormState = { countryId: string; cityId: string; subjectName: string; subjectContactPhone: string; concernDescription: string; applicantPhoneE164: string; otpCode: string };

const API = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";
const INITIAL: FormState = { countryId: "", cityId: "", subjectName: "", subjectContactPhone: "", concernDescription: "", applicantPhoneE164: "", otpCode: "" };

async function request<T>(path: string, body?: unknown): Promise<T> {
  const response = await fetch(`${API}${path}`, {
    method: body === undefined ? "GET" : "POST",
    headers: body === undefined ? undefined : { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
    cache: "no-store",
  });
  if (!response.ok) {
    const data = await response.json().catch(() => ({})) as { message?: string; errors?: { message: string }[] };
    throw new Error(data.errors?.map((error) => error.message).join("، ") || data.message || "تعذّر إتمام الطلب الآن. حاول مرة أخرى لاحقًا.");
  }
  return response.status === 204 ? undefined as T : await response.json() as T;
}

export function AtmaenRequestForm() {
  const [countries, setCountries] = useState<Country[]>([]);
  const [cities, setCities] = useState<City[]>([]);
  const [form, setForm] = useState(INITIAL);
  const [step, setStep] = useState<Step>("details");
  const [consented, setConsented] = useState(false);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    let live = true;
    void request<Country[]>("/api/v1/geography/countries")
      .then((items) => { if (live) setCountries(items); })
      .catch((reason: unknown) => { if (live) setError(reason instanceof Error ? reason.message : "تعذّر تحميل دليل الدول."); })
      .finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, []);

  useEffect(() => {
    if (!form.countryId) { setCities([]); return; }
    let live = true;
    setForm((current) => ({ ...current, cityId: "" }));
    void request<City[]>(`/api/v1/geography/countries/${encodeURIComponent(form.countryId)}/cities`)
      .then((items) => { if (live) setCities(items); })
      .catch((reason: unknown) => { if (live) setError(reason instanceof Error ? reason.message : "تعذّر تحميل المدن."); });
    return () => { live = false; };
  }, [form.countryId]);

  function change<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((current) => ({ ...current, [key]: value }));
    setError("");
  }

  async function sendCode(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError("");
    if (!consented) { setError("يرجى الموافقة على استخدام البيانات لمعالجة البلاغ قبل المتابعة."); return; }
    setBusy(true);
    try {
      await request("/api/v1/atmaen/verification-requests", { applicantPhoneE164: form.applicantPhoneE164.trim() });
      setStep("verify");
    } catch (reason) { setError(reason instanceof Error ? reason.message : "تعذّر إرسال رمز التحقق."); }
    finally { setBusy(false); }
  }

  async function submitCase(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError(""); setBusy(true);
    try {
      await request("/api/v1/atmaen/cases", {
        countryId: form.countryId, cityId: form.cityId, subjectName: form.subjectName.trim(),
        subjectContactPhone: form.subjectContactPhone.trim() || null, concernDescription: form.concernDescription.trim(),
        applicantPhoneE164: form.applicantPhoneE164.trim(), otpCode: form.otpCode.trim(),
      });
      setStep("success");
    } catch (reason) { setError(reason instanceof Error ? reason.message : "تعذّر إرسال البلاغ."); }
    finally { setBusy(false); }
  }

  return <section className="atmaen-form-card" aria-labelledby="atmaen-form-title">
    <div className="atmaen-form-head"><span className="atmaen-form-icon" aria-hidden="true">🛟</span><div><span className="atmaen-form-eyebrow">خدمة جسور المجتمعية</span><h2 id="atmaen-form-title">اطمّن على مغتربك</h2><p>أرسل بلاغًا آمنًا عن شخص تريد الاطمئنان عليه. لن تظهر بيانات البلاغ للعموم.</p></div></div>
    {step !== "success" && <ol className="atmaen-steps" aria-label="خطوات إرسال البلاغ"><li className={step === "details" ? "current" : "done"}><span>١</span><span>تفاصيل البلاغ</span></li><li className={step === "verify" ? "current" : ""}><span>٢</span><span>تأكيد رقمك</span></li></ol>}
    {error && <div className="atmaen-form-error" role="alert">{error}</div>}
    {loading ? <div className="atmaen-form-loading" role="status">جارٍ تحميل دليل الدول…</div> : countries.length === 0 ? <div className="atmaen-form-error" role="status">خدمة البلاغات قيد التجهيز في هذه البيئة. يجب على إدارة جسور إضافة الدول والمدن من دليل المواقع قبل استقبال البلاغات.</div> : step === "success" ? <div className="atmaen-form-success" role="status"><span aria-hidden="true">✓</span><h3>تم استلام البلاغ</h3><p>وصلت المعلومات إلى فريق المتابعة المختص. لا تشارك بيانات الحالة أو أرقام التواصل علنًا.</p><button type="button" className="atmaen-submit" onClick={() => { setForm(INITIAL); setConsented(false); setStep("details"); }}>إرسال بلاغ آخر</button></div> : step === "details" ? <form className="atmaen-form" onSubmit={(event) => void sendCode(event)}>
      <div className="atmaen-form-grid">
        <label><span>الدولة التي يوجد بها الشخص <b>*</b></span><select required value={form.countryId} onChange={(event) => change("countryId", event.target.value)}><option value="">اختر الدولة</option>{countries.map((country) => <option key={country.id} value={country.id}>{country.nameAr}</option>)}</select></label>
        <label><span>المدينة <b>*</b></span><select required value={form.cityId} onChange={(event) => change("cityId", event.target.value)} disabled={!form.countryId || cities.length === 0}><option value="">{!form.countryId ? "اختر الدولة أولًا" : cities.length ? "اختر المدينة" : "لا توجد مدن مسجلة"}</option>{cities.map((city) => <option key={city.id} value={city.id}>{city.nameAr}</option>)}</select></label>
        <label><span>اسم الشخص الذي تبحث عنه <b>*</b></span><input required maxLength={160} autoComplete="name" value={form.subjectName} onChange={(event) => change("subjectName", event.target.value)} /></label>
        <label><span>رقم تواصل الشخص (اختياري)</span><input type="tel" dir="ltr" autoComplete="tel" placeholder="+1…" value={form.subjectContactPhone} onChange={(event) => change("subjectContactPhone", event.target.value)} /></label>
        <label className="wide"><span>ما الذي يقلقك؟ <b>*</b></span><textarea required minLength={10} maxLength={2000} rows={4} value={form.concernDescription} onChange={(event) => change("concernDescription", event.target.value)} placeholder="اشرح بإيجاز سبب طلب المساعدة…" /><small>{form.concernDescription.length} / 2000</small></label>
        <label className="wide"><span>رقم هاتفك لإرسال رمز التأكيد <b>*</b></span><input required type="tel" dir="ltr" autoComplete="tel" pattern="\+[1-9]\d{7,14}" placeholder="+201XXXXXXXXX" value={form.applicantPhoneE164} onChange={(event) => change("applicantPhoneE164", event.target.value)} /><small>اكتب الرقم بالصيغة الدولية، مثل +201XXXXXXXXX.</small></label>
      </div>
      <label className="atmaen-consent"><input type="checkbox" checked={consented} onChange={(event) => setConsented(event.target.checked)} /><span>أوافق على استخدام بيانات التواصل والمعلومات أعلاه لمعالجة هذا البلاغ والتواصل معي بشأنه. <strong>لا ترسل معلومات طبية أو مالية أو كلمات مرور.</strong></span></label>
      <button className="atmaen-submit" disabled={busy || countries.length === 0 || cities.length === 0}>{busy ? "جارٍ إرسال الرمز…" : "إرسال رمز التأكيد"}<span aria-hidden="true">←</span></button>
    </form> : <form className="atmaen-form" onSubmit={(event) => void submitCase(event)}>
      <div className="atmaen-verify-callout"><span aria-hidden="true">✉</span><p>أرسلنا رمز التحقق إلى <b dir="ltr">{form.applicantPhoneE164}</b>. أدخله لتأكيد رقمك وإرسال البلاغ.</p></div>
      <label className="atmaen-otp-field"><span>رمز التأكيد</span><input required dir="ltr" inputMode="numeric" autoComplete="one-time-code" pattern="\d{6}" minLength={6} maxLength={6} value={form.otpCode} onChange={(event) => change("otpCode", event.target.value.replace(/\D/g, "").slice(0, 6))} placeholder="000000" /></label>
      <div className="atmaen-verify-actions"><button type="button" className="atmaen-back" onClick={() => { setStep("details"); setError(""); }}>تعديل البيانات</button><button className="atmaen-submit" disabled={busy}>{busy ? "جارٍ إرسال البلاغ…" : "تأكيد وإرسال البلاغ"}<span aria-hidden="true">←</span></button></div>
    </form>}
    <p className="atmaen-form-foot">جسور منصة إعلامية وليست جهة طوارئ. إذا كان هناك خطر مباشر، تواصل مع خدمات الطوارئ المحلية والسفارة المصرية.</p>
  </section>;
}
