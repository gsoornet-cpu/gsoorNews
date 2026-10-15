"use client";

import { useRef } from "react";

interface Service {
  icon: string;
  title: string;
  description: string;
  href: string;
  featured?: boolean;
}

const services: Service[] = [
  { icon: "🆘", title: "اطمّن على مغتربك", description: "استجابة وتتبع عاجل", href: "#atmaen", featured: true },
  { icon: "🗺️", title: "خريطة المهجر", description: "توزيع الجاليات", href: "/category?cat=mughtarib" },
  { icon: "🔔", title: "تنبيهات جغرافية", description: "إشعارات الطوارئ", href: "#city-alerts" },
  { icon: "⚽", title: "الرياضيون", description: "متابعة المحترفين", href: "/category?cat=sports" },
  { icon: "🏛️", title: "دليل السفارات", description: "المعاملات والتواصل", href: "/category?cat=official" },
  { icon: "👤", title: "ملف جسور", description: "أخبار تهمك", href: "/category?cat=mughtarib" },
  { icon: "💼", title: "فرص العمل", description: "وظائف دولية", href: "/category?cat=opportunities" },
  { icon: "🎓", title: "الجيل الثاني", description: "برامج وقصص ملهمة", href: "/category?cat=secondgen" },
];

/**
 * Same markup/classes as the Final Demo's services band. It is a Client
 * Component because the arrow buttons scroll the row (event handlers cannot
 * be rendered from the Server Component home page).
 */
export function ServicesBand() {
  const rowRef = useRef<HTMLDivElement>(null);
  const scroll = (direction: 1 | -1) => {
    const row = rowRef.current;
    if (!row) return;
    const card = row.querySelector<HTMLElement>(".svc-card");
    const rtl = getComputedStyle(row).direction === "rtl";
    const distance = card ? card.offsetWidth + 16 : row.clientWidth * 0.8;
    row.scrollBy({ left: direction * (rtl ? -1 : 1) * distance, behavior: "smooth" });
  };

  return (
    <section className="services-band" id="services" aria-labelledby="services-heading">
      <div className="wrap">
        <div className="services-header-wrap">
          <div className="section-head-title">
            <h2 className="eyebrow-tag" id="services-heading">منظومة الخدمات</h2>
          </div>

        </div>

        <div className="svc-slider-container">
          <div className="svc-row" id="svcRow" ref={rowRef}>
            {services.map((service) => (
              <a className={`svc-card${service.featured ? " featured" : ""}`} href={service.href} key={service.title}>
                <div className="icon-box">
                  <span className="emoji-icon">{service.icon}</span>
                  {service.featured && <div className="glow-effect" />}
                </div>
                <div className="svc-content">
                  <span className="svc-title">{service.title}</span>
                  <span className="svc-desc">{service.description}</span>
                </div>
              </a>
            ))}
          </div>
        </div>
        <div className="svc-controls" role="group" aria-label="التنقل بين الخدمات">
          <button className="svc-arrow-btn prev" type="button" aria-label="الخدمة السابقة" onClick={() => scroll(-1)}>
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><path d="M9 18l6-6-6-6" /></svg>
          </button>
          <button className="svc-arrow-btn next" type="button" aria-label="الخدمة التالية" onClick={() => scroll(1)}>
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><path d="M15 18l-6-6 6-6" /></svg>
          </button>
        </div>
      </div>
    </section>
  );
}
