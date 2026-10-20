import type { Metadata } from "next";
import Link from "next/link";
import { getPublicCountries, getPublicCrisisMode } from "@/lib/news-api";

export const dynamic = "force-dynamic";
export const metadata: Metadata = {
  title: "مركز الأزمة | جسور",
  description: "التنبيهات التحريرية الموثقة للمصريين في الخارج.",
  robots: { index: false, follow: true },
};

export default async function CrisisPage() {
  const [crisisResult, countriesResult] = await Promise.allSettled([getPublicCrisisMode(), getPublicCountries()]);
  const crisis = crisisResult.status === "fulfilled" ? crisisResult.value : null;
  const countries = countriesResult.status === "fulfilled" ? countriesResult.value : [];
  const affected = new Set(crisis?.affectedCountryIds ?? []);
  const affectedCountries = countries.filter((country) => affected.has(country.id));
  const severity = crisis?.severity === "emergency" ? "طوارئ" : crisis?.severity === "advisory" ? "تنبيه" : "عاجل";

  return <main id="main" className="crisis-page">
    {crisis?.isActive ? <>
      <section className="crisis-hero"><div className="wrap">
        <span className="crisis-hero__eyebrow"><i className="crisis-bar__dot" aria-hidden="true" />{severity} · مركز الأزمة</span>
        <h1 className="crisis-hero__title">{crisis.title}</h1>
        <p className="crisis-hero__summary">{crisis.summary}</p>
        {affectedCountries.length > 0 && <div className="crisis-country-list" aria-label="الدول المتأثرة">{affectedCountries.map((country) => <span key={country.id}>{country.nameAr}</span>)}</div>}
        <p className="crisis-hero__meta">تحديث جسور: {crisis.lastModifiedAtUtc ? new Intl.DateTimeFormat("ar-EG", { dateStyle: "full", timeStyle: "short" }).format(new Date(crisis.lastModifiedAtUtc)) : "آخر تحديث"}</p>
        <p className="crisis-hero__source">المصدر: <a href={crisis.sourceUrl} target="_blank" rel="noopener noreferrer">{crisis.sourceName} <span aria-hidden="true">↗</span></a></p>
        <div className="crisis-hero__actions"><a className="primary" href="#crisis-guidance">إرشادات المتابعة</a><Link className="ghost" href="/">العودة إلى الرئيسية</Link></div>
      </div></section>
      <section id="crisis-guidance" className="section"><div className="wrap"><article className="crisis-action-card"><span className="crisis-spotlight__eyebrow">معلومات مهمة</span><h2>تابع المصادر الرسمية</h2><p>جسور منصة إعلامية للمعلومات والمتابعة، وليست جهة طوارئ أو استجابة رسمية. اتبع تعليمات السلطات المحلية والسفارة المصرية، وتحقق من تاريخ كل معلومة قبل تداولها.</p><p>لا تنشر أرقام هواتف أو عناوين أو بيانات شخصية علنًا. البلاغات الفردية لا تظهر هنا ولا تُستخدم لإنتاج أعداد عامة.</p></article></div></section>
    </> : <section className="section crisis-empty-state"><div className="wrap"><div className="crisis-action-card"><span className="crisis-empty-icon" aria-hidden="true">✓</span><h1>لا توجد أزمة نشطة حاليًا</h1><p>يُفعّل مركز الأزمة من غرفة أخبار جسور عند اعتماد تنبيه للدول المتأثرة.</p><Link className="cms-btn primary" href="/">العودة إلى الرئيسية</Link></div></div></section>}
  </main>;
}
