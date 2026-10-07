import Image from "next/image";
import Link from "next/link";

const SOCIAL_LINKS = [
  { name: "facebook" as const, label: "فيسبوك", path: "M14 8h3V4h-3c-3.31 0-5 1.69-5 5v3H6v4h3v4h4v-4h3l1-4h-4V9c0-.66.34-1 1-1z" },
  { name: "x-twitter" as const, label: "X", path: "M18.9 2H22l-6.77 7.74L23.2 22h-6.24l-4.89-6.39L6.48 22H3.37l7.24-8.28L2.8 2h6.4l4.42 5.84L18.9 2zm-1.1 17.7h1.73L8.27 4.2H6.42L17.8 19.7z" },
];

const SITE_LINKS = [
  { href: "/", label: "الرئيسية" },
  { href: "/category?cat=mughtarib", label: "أخبار المغتربين" },
  { href: "/category?cat=egypt", label: "أخبار مصر" },
  { href: "/category?cat=success", label: "قصة نجاح" },
  { href: "/category?cat=official", label: "مع مسئول" },
  { href: "/category?cat=red", label: "خط أحمر" },
];

const MORE_LINKS = [
  { href: "/category?cat=lamma", label: "اللمة الحلوة" },
  { href: "/category?cat=sports", label: "رياضة" },
  { href: "/category?cat=secondgen", label: "الجيل الثاني" },
  { href: "/category?cat=articles", label: "مقالات رأي" },
];

const SERVICE_LINKS = [
  { href: "/atmaen", label: "اطمّن على مغتربك" },
  { href: "/country?c=ae", label: "خريطة المهجر" },
  { href: "/category?cat=opportunities", label: "فرص العمل" },
  { href: "/auth", label: "بروفايل المهجر" },
];

const ABOUT_LINKS = [
  { href: "/about", label: "عن منصة جسور" },
  { href: "/privacy", label: "سياسة الخصوصية" },
];

export function SiteFooter() {
  return (
    <footer className="foot-v2">
      <div className="wrap foot-v2-top">
        <div className="foot-v2-grid">
          <div className="foot-v2-brand">
            <Link href="/">
              <Image className="logo-img" src="/assets/josour-logo.png" alt="جسور" width={130} height={38} />
            </Link>
            <p>منصة المصريين بالمهجر — إخبار، ذكاء استخباراتي جغرافي، وشبكة اطمئنان الأسرة.</p>

            <div className="foot-v2-social social-icons">
              {SOCIAL_LINKS.map((social) => (
                <a key={social.name} href="#" aria-label={social.label} className={social.name}>
                  <svg viewBox="0 0 24 24" aria-hidden="true">
                    <path d={social.path} />
                  </svg>
                </a>
              ))}
            </div>
          </div>

          <div className="foot-v2-col">
            <h5>أقسام الموقع</h5>
            <ul>
              {SITE_LINKS.map((link) => (
                <li key={link.href}><Link href={link.href}>{link.label}</Link></li>
              ))}
            </ul>
          </div>

          <div className="foot-v2-col">
            <h5>&nbsp;</h5>
            <ul>
              {MORE_LINKS.map((link) => (
                <li key={link.href}><Link href={link.href}>{link.label}</Link></li>
              ))}
            </ul>
          </div>

          <div className="foot-v2-col">
            <h5>الخدمات</h5>
            <ul>
              {SERVICE_LINKS.map((link) => (
                <li key={link.href}><Link href={link.href}>{link.label}</Link></li>
              ))}
            </ul>
          </div>

          <div className="foot-v2-col">
            <h5>من نحن</h5>
            <ul>
              {ABOUT_LINKS.map((link) => (
                <li key={link.href}><Link href={link.href}>{link.label}</Link></li>
              ))}
            </ul>
          </div>
        </div>
      </div>

      <div className="wrap foot-v2-bottom">
        <span>© ٢٠٢٦ جسور. جميع الحقوق محفوظة. — منصة المصريين بالمهجر</span>
        <div className="foot-v2-bottom-links">
          <Link href="/privacy">الخصوصية</Link>
        </div>
        <select className="foot-v2-lang" aria-label="اختر اللغة" defaultValue="العربية">
          <option>العربية</option>
          <option>English</option>
          <option>Français</option>
        </select>
      </div>
    </footer>
  );
}
