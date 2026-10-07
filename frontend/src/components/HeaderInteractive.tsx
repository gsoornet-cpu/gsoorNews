"use client";

import { useEffect, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { TopbarClock } from "./TopbarClock";

const SOCIAL_LINKS = [
  { name: "facebook" as const, label: "فيسبوك", path: "M14 8h3V4h-3c-3.31 0-5 1.69-5 5v3H6v4h3v4h4v-4h3l1-4h-4V9c0-.66.34-1 1-1z" },
  { name: "x-twitter" as const, label: "X", path: "M18.9 2H22l-6.77 7.74L23.2 22h-6.24l-4.89-6.39L6.48 22H3.37l7.24-8.28L2.8 2h6.4l4.42 5.84L18.9 2zm-1.1 17.7h1.73L8.27 4.2H6.42L17.8 19.7z" },
];

const NAV_LINKS = [
  { href: "/", label: "الرئيسية" },
  { href: "/category?cat=mughtarib", label: "أخبار المغتربين" },
  { href: "/category?cat=success", label: "قصة نجاح" },
  { href: "/category?cat=egypt", label: "أخبار مصر" },
  { href: "/category?cat=opportunities", label: "فرص استثمارية" },
  { href: "/category?cat=official", label: "مع مسئول" },
  { href: "/category?cat=red", label: "خط أحمر" },
];

const MORE_LINKS = [
  { href: "/category?cat=events", label: "فعاليات" },
  { href: "/category?cat=lamma", label: "اللمة الحلوة" },
  { href: "/category?cat=secondgen", label: "الجيل الثاني" },
  { href: "/category?cat=sports", label: "رياضة" },
  { href: "/category?cat=arts", label: "فنون" },
  { href: "/category?cat=articles", label: "مقالات رأي" },
  { href: "/category?cat=various", label: "منوعات" },
  { href: "/videos", label: "فيديو" },
];

/**
 * The demo's header/nav/topbar/ticker/drawer/search-overlay, reimplemented
 * as one client component (real markup 1:1 with index.html — see the class
 * names — not app.js's DOM-templating). All of it lives in one component
 * because the burger, the two search triggers, the drawer, and the search
 * panel all share open/close state that would otherwise need a context for
 * five lines of actual logic.
 *
 * tickerHeadlines comes from the server-rendered parent (real Story
 * titles) — this component never fetches data itself, matching the
 * project's "Server Components fetch, Client Components render" split.
 */
export function HeaderInteractive({ tickerHeadlines }: { tickerHeadlines: string[] }) {
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");
  const [suggestions, setSuggestions] = useState<Array<{ id: string; title: string; slug: string }>>([]);
  const [suggestionsLoading, setSuggestionsLoading] = useState(false);

  useEffect(() => {
    const nav = document.querySelector<HTMLElement>(".bluebarr");
    const header = document.querySelector<HTMLElement>("header.site");
    if (!nav || !header) return;
    const observer = new IntersectionObserver(([entry]) => nav.classList.toggle("is-scrolled", !entry.isIntersecting));
    observer.observe(header);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    const query = searchQuery.trim();
    if (!searchOpen || query.length < 2) {
      setSuggestions([]);
      setSuggestionsLoading(false);
      return;
    }
    const controller = new AbortController();
    const timer = window.setTimeout(async () => {
      setSuggestionsLoading(true);
      try {
        const response = await fetch(`/api/search?q=${encodeURIComponent(query)}&limit=5`, { signal: controller.signal, cache: "no-store" });
        if (!response.ok) throw new Error("Search failed");
        const data = await response.json() as { items: Array<{ id: string; title: string; slug: string }> };
        setSuggestions(data.items);
      } catch {
        if (!controller.signal.aborted) setSuggestions([]);
      } finally {
        if (!controller.signal.aborted) setSuggestionsLoading(false);
      }
    }, 220);
    return () => { window.clearTimeout(timer); controller.abort(); };
  }, [searchOpen, searchQuery]);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        setSearchOpen((open) => !open);
      } else if (event.key === "Escape") {
        setSearchOpen(false);
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  // The demo repeats the ticker group 3-4x in the HTML itself for a
  // seamless CSS marquee loop — same technique, applied to real headlines
  // instead of the demo's fixed sample list.
  const tickerGroups = tickerHeadlines.length > 0 ? [0, 1, 2] : [];

  return (
    <>
      <div className="topbar">
        <div className="wrap">
          <div className="right topbar-info">
            <TopbarClock />
            <select className="lang" aria-label="اختر اللغة" defaultValue="العربية">
              <option>العربية</option>
              <option>English</option>
              <option>Français</option>
            </select>
          </div>
          <div className="left social-icons">
            {SOCIAL_LINKS.map((social) => (
              <a key={social.name} href="#" aria-label={social.label} className={social.name}>
                <svg viewBox="0 0 24 24" aria-hidden="true">
                  <path d={social.path} />
                </svg>
              </a>
            ))}
          </div>
        </div>
      </div>

      <header className="site">
        <div className="wrap header-inner">
          <button type="button" className="icon-btn burger" aria-label="فتح القائمة" onClick={() => setDrawerOpen(true)}>
            ☰
          </button>

          <Link className="logo" href="/">
            <Image className="logo-img" src="/assets/josour-logo.png" alt="جسور" width={110} height={32} priority />
            <span className="logo-slogan">صوت المصريين في الخارج</span>
          </Link>

          <div className="header-actions">
            <button type="button" className="icon-btn" aria-label="بحث" onClick={() => setSearchOpen(true)}>
              🔍
            </button>
          </div>
        </div>
      </header>

      <nav className="bluebarr">
        <div className="nav-container">
          <div className="nav-brand-group">
            <button
              type="button"
              className="bluebarr-burger"
              aria-label="فتح قائمة التنقل"
              onClick={() => setDrawerOpen(true)}
            >
              <span></span><span></span><span></span>
            </button>
            <div className="nav-sticky-logo">
              <Link href="/" aria-label="الرئيسية">
                <Image src="/assets/josour-logo.png" alt="جسور" width={90} height={26} />
              </Link>
            </div>

            <ul className="nav-links">
              {NAV_LINKS.map((link) => (
                <li key={link.href}>
                  <Link href={link.href} className="nav-link">
                    {link.label}
                  </Link>
                </li>
              ))}
              <li className="dropdown">
                <button type="button" className="nav-link dropdown-btn">المزيد</button>
                <ul className="dropdown-menu">
                  {MORE_LINKS.map((link) => (
                    <li key={link.href}><Link href={link.href}>{link.label}</Link></li>
                  ))}
                </ul>
              </li>
            </ul>
          </div>

          <div className="nav-sticky-actions">
            <button
              type="button"
              className="nav-action-btn search-trigger"
              aria-label="بحث"
              onClick={() => setSearchOpen(true)}
            >
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2}>
                <circle cx="11" cy="11" r="8" />
                <line x1="21" y1="21" x2="16.65" y2="16.65" />
              </svg>
            </button>
            <div className="auth-group">
              <Link href="/cms/login" className="btn-nav btn-ghostt">دخول هيئة التحرير</Link>
            </div>
          </div>
        </div>
      </nav>

      {tickerGroups.length > 0 && (
        <div className="bluebar">
          <div className="ticker">
            <div className="ticker-track">
              {tickerGroups.map((groupIndex) => (
                <div className="ticker-group" key={groupIndex}>
                  {tickerHeadlines.map((headline, i) => (
                    <span key={`${groupIndex}-${i}`}>{headline}</span>
                  ))}
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      <div
        className={`drawer-overlay${drawerOpen ? " open" : ""}`}
        onClick={(e) => e.target === e.currentTarget && setDrawerOpen(false)}
      >
        <div className="drawer">
          <div className="flex" style={{ justifyContent: "space-between", alignItems: "center", marginBottom: 20 }}>
            <Image className="logo-img" src="/assets/josour-logo.png" alt="جسور" width={110} height={32} />
            <button type="button" className="drawer-close icon-btn" onClick={() => setDrawerOpen(false)}>✕</button>
          </div>
          <nav>
            <Link href="/">الرئيسية</Link>
            {NAV_LINKS.slice(1).map((link) => <Link key={link.href} href={link.href}>{link.label}</Link>)}
            {MORE_LINKS.map((link) => <Link key={link.href} href={link.href}>{link.label}</Link>)}
            <Link href="/cms/login">دخول هيئة التحرير</Link>
          </nav>
        </div>
      </div>

      <div className={`search-overlay${searchOpen ? " is-active" : ""}`} onMouseDown={(event) => { if (event.target === event.currentTarget) setSearchOpen(false); }}>
        <div className={`search-panel${searchOpen ? " is-active" : ""}`} role="dialog" aria-modal="true" aria-label="بحث في الأخبار">
          <form action="/search" method="get" style={{ display: "flex", gap: 8 }}>
            <input
              type="text"
              name="q"
              placeholder="ابحث عن خبر، دولة، أو خدمة..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              autoFocus={searchOpen}
            />
            <button type="submit" className="icon-btn" aria-label="تنفيذ البحث">🔍</button>
            <button type="button" className="icon-btn" aria-label="إغلاق البحث" onClick={() => setSearchOpen(false)}>✕</button>
          </form>
          <div className="search-suggest" aria-live="polite">
            {suggestionsLoading && <p className="row">جارٍ البحث…</p>}
            {!suggestionsLoading && searchQuery.trim().length >= 2 && suggestions.map((item) => (
              <Link className="row" key={item.id} href={`/article/${item.id}/${encodeURIComponent(item.slug)}`} onClick={() => setSearchOpen(false)}>{item.title}</Link>
            ))}
            {!suggestionsLoading && searchQuery.trim().length >= 2 && suggestions.length === 0 && <p className="row">لا توجد نتائج مطابقة حتى الآن.</p>}
            {searchQuery.trim().length >= 2 && <Link className="row" href={`/search?q=${encodeURIComponent(searchQuery.trim())}`} onClick={() => setSearchOpen(false)}>عرض كل نتائج البحث ←</Link>}
          </div>
        </div>
      </div>
    </>
  );
}
