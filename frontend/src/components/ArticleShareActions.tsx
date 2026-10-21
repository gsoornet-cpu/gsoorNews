"use client";

import { useState } from "react";

function Icon({ name }: { name: "share" | "copy" | "print" | "facebook" | "whatsapp" | "x" }) {
  const paths = {
    share: <><circle cx="18" cy="5" r="3" /><circle cx="6" cy="12" r="3" /><circle cx="18" cy="19" r="3" /><path d="m8.7 10.6 6.6-4.2M8.7 13.4l6.6 4.2" /></>,
    copy: <><rect x="8" y="8" width="12" height="12" rx="2" /><path d="M16 8V5a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h3" /></>,
    print: <><path d="M6 9V3h12v6M6 17H4a2 2 0 0 1-2-2v-4a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v4a2 2 0 0 1-2 2h-2" /><path d="M6 14h12v7H6z" /></>,
    facebook: <path d="M14 8h3V4h-3c-3.3 0-5 1.7-5 5v3H6v4h3v5h4v-5h3l1-4h-4V9c0-.7.3-1 1-1Z" />,
    whatsapp: <><path d="M20.5 11.5a8.5 8.5 0 0 1-12.6 7.4L3 20l1.2-4.7A8.5 8.5 0 1 1 20.5 11.5Z" /><path d="M8 8.4c.3-.5.6-.5 1-.5h.4c.2 0 .4.1.5.4l.8 1.8c.1.2 0 .4-.1.6l-.6.7c-.2.2-.2.4 0 .6.6 1 1.4 1.8 2.5 2.3.2.1.4.1.6-.1l.8-.9c.2-.2.4-.2.6-.1l1.7.8c.3.1.4.3.4.5 0 .6-.3 1.2-.8 1.6-.6.5-1.3.7-2.1.5-1.2-.2-2.6-1-3.8-2.2-1.3-1.2-2-2.6-2.2-3.7-.2-.9.1-1.7.5-2.3Z" /></>,
    x: <path d="M18.9 2H22l-6.8 7.8L23.2 22h-6.3l-4.9-6.4L6.5 22H3.4l7.2-8.3L2.8 2h6.4l4.4 5.8L18.9 2Zm-1.1 17.7h1.7L8.3 4.2H6.4l11.4 15.5Z" />,
  };
  return <svg viewBox="0 0 24 24" aria-hidden="true" fill={name === "facebook" || name === "x" ? "currentColor" : "none"} stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">{paths[name]}</svg>;
}

export function ArticleShareActions({ title }: { title: string }) {
  const [message, setMessage] = useState("");

  async function copyLink() {
    try {
      await navigator.clipboard.writeText(window.location.href);
      setMessage("تم نسخ رابط الخبر");
    } catch {
      setMessage("تعذّر نسخ الرابط من هذا المتصفح");
    }
  }

  async function share() {
    if (!navigator.share) return copyLink();
    try {
      await navigator.share({ title, text: title, url: window.location.href });
      setMessage("تم فتح خيارات المشاركة");
    } catch (error) {
      if (error instanceof Error && error.name !== "AbortError") setMessage("تعذّرت المشاركة من هذا المتصفح");
    }
  }

  function openShare(network: "facebook" | "whatsapp" | "x") {
    const url = window.location.href;
    const encodedUrl = encodeURIComponent(url);
    const encodedTitle = encodeURIComponent(title);
    const target = network === "facebook"
      ? `https://www.facebook.com/sharer/sharer.php?u=${encodedUrl}`
      : network === "whatsapp"
        ? `https://wa.me/?text=${encodedTitle}%20${encodedUrl}`
        : `https://twitter.com/intent/tweet?text=${encodedTitle}&url=${encodedUrl}`;
    window.open(target, "_blank", "noopener,noreferrer,width=640,height=600");
  }

  return <div className="article-share" aria-label="مشاركة الخبر">
    <span className="article-share__label">شارك الخبر</span>
    <div className="article-share__actions">
      <button type="button" onClick={() => void share()} aria-label="مشاركة الخبر" title="مشاركة الخبر"><Icon name="share" /></button>
      <button type="button" onClick={() => void copyLink()} aria-label="نسخ رابط الخبر" title="نسخ الرابط"><Icon name="copy" /></button>
      <button type="button" onClick={() => window.print()} aria-label="طباعة الخبر" title="طباعة"><Icon name="print" /></button>
      <button type="button" className="is-facebook" onClick={() => openShare("facebook")} aria-label="مشاركة على فيسبوك" title="فيسبوك"><Icon name="facebook" /></button>
      <button type="button" className="is-whatsapp" onClick={() => openShare("whatsapp")} aria-label="مشاركة على واتساب" title="واتساب"><Icon name="whatsapp" /></button>
      <button type="button" className="is-x" onClick={() => openShare("x")} aria-label="مشاركة على X" title="X"><Icon name="x" /></button>
    </div>
    <span className="article-share__status" aria-live="polite">{message}</span>
  </div>;
}
