import type { Metadata } from "next";
import Link from "next/link";
import { AtmaenRequestForm } from "@/components/AtmaenRequestForm";

export const metadata: Metadata = {
  title: "اطمّن على مغتربك | جسور",
  description: "أرسل بلاغًا خاصًا لفريق جسور لمساعدتك على الاطمئنان على مغترب.",
  robots: { index: false, follow: true },
};

export default function AtmaenPage() {
  return <main id="main" className="atmaen-page">
    <section className="atmaen-page-hero"><div className="wrap"><Link className="atmaen-page-back" href="/">← الرئيسية</Link><span className="atmaen-page-kicker">جسور معك وقت القلق</span><h1>لأن الاطمئنان حق</h1><p>أرسل معلوماتك لفريق المتابعة بسرية. نتحقق من رقمك أولًا، ثم تصل التفاصيل إلى محرر مختص لمراجعتها.</p><div className="atmaen-page-points"><span><i aria-hidden="true">✓</i> بلاغ خاص لا يظهر للعموم</span><span><i aria-hidden="true">✓</i> متابعة من محرر مختص</span><span><i aria-hidden="true">✓</i> لا ننشر بيانات التواصل</span></div></div></section>
    <div className="wrap atmaen-page-content"><AtmaenRequestForm /><aside className="atmaen-page-aside"><div><span aria-hidden="true">ℹ</span><h2>قبل إرسال البلاغ</h2><ul><li>اكتب بيانات دقيقة تساعد فريق المتابعة.</li><li>أدخل رقمًا دوليًا تستطيع استلام رمز التأكيد عليه.</li><li>لا ترسل كلمات مرور أو معلومات مالية.</li><li>الخدمة ليست بديلًا عن الطوارئ أو الجهات الرسمية.</li></ul><Link href="/crisis">مركز الأزمة والتنبيهات ←</Link></div><div className="atmaen-page-aside__support"><strong>هل يوجد خطر مباشر؟</strong><p>اتصل بخدمات الطوارئ المحلية أو بالسفارة المصرية في بلد الشخص.</p></div></aside></div>
  </main>;
}
