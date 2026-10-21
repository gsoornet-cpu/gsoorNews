import type { Metadata } from "next";
import Link from "next/link";

export const metadata: Metadata = {
  title: "عن منصة جسور | جسور",
  description: "تعرف على منصة جسور وأقسامها وخدماتها للمصريين بالخارج.",
};

export default function AboutPage() {
  return <main id="main" className="info-page"><section className="info-page-hero"><div className="wrap">
    <nav className="breadcrumb" aria-label="مسار الصفحة"><Link className="breadcrumb-home" href="/">الرئيسية</Link><span className="breadcrumb-separator" aria-hidden="true">›</span><span aria-current="page">عن جسور</span></nav>
    <span className="eyebrow">صوت المصريين في الخارج</span><h1>جسور تجمع الخبر والخدمة</h1>
    <p>منصة عربية تتابع أخبار المصريين بالخارج، وتقدم مساحة تحريرية للأخبار والقصص والخدمات المرتبطة بالجاليات.</p>
  </div></section><section className="section"><div className="wrap info-page-content">
    <article><h2>ما الذي نقدمه؟</h2><p>ننشر الأخبار التي يراجعها فريق التحرير ويعتمدها للنشر، وننظمها في أقسام تساعد القارئ على الوصول إلى ما يهمه. كما تتيح المنصة خدمة «اطمّن» لإرسال طلب متابعة خاص، وخريطة تعرض بيانات منشورة بمصادرها عندما تتوفر.</p><p>المعلومات المنشورة على الخريطة تقديرية بحسب مصادرها، ولا تمثل إحصاءً حكوميًا. ولا تعرض الخريطة مواقع أو هويات مستخدمي جسور.</p></article>
    <div className="info-page-links"><Link href="/category?cat=mughtarib">أخبار المغتربين ←</Link><Link href="/atmaen">اطمّن على مغتربك ←</Link><Link href="/diaspora-map">خريطة المهجر ←</Link><Link href="/videos">فيديوهات جسور ←</Link></div>
  </div></section></main>;
}
