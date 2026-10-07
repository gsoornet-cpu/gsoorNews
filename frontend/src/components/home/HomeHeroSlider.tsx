"use client";

import { useCallback, useEffect, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { formatArabicDate } from "@/lib/format";
import { VideoModal } from "./VideoModal";

export interface HomeHeroSlide {
  id: string;
  title: string;
  excerpt?: string | null;
  href: string;
  imageUrl?: string | null;
  /** Present ONLY for real published videos. Slides without it are plain news and get no play UI. */
  videoUrl?: string;
  publishedAtUtc?: string | null;
}

/** 95 -> "1:35", 3725 -> "1:02:05". Returns null for unusable values (live streams, NaN). */
function formatDuration(seconds: number): string | null {
  if (!Number.isFinite(seconds) || seconds <= 0) return null;
  const total = Math.round(seconds);
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  const pad = (n: number) => String(n).padStart(2, "0");
  return h ? `${h}:${pad(m)}:${pad(s)}` : `${pad(m)}:${pad(s)}`;
}

export function HomeHeroSlider({ slides }: { slides: HomeHeroSlide[] }) {
  const [active, setActive] = useState(0);
  const [modalOpen, setModalOpen] = useState(false);
  const [durations, setDurations] = useState<Record<string, string>>({});
  const current = slides[active];
  const closeModal = useCallback(() => setModalOpen(false), []);

  // Auto-advance, paused while the player is open.
  useEffect(() => {
    if (slides.length < 2 || modalOpen) return;
    const timer = window.setInterval(() => setActive((index) => (index + 1) % slides.length), 8000);
    return () => window.clearInterval(timer);
  }, [slides.length, modalOpen]);

  // The platform does not store video length, so read it from each video's metadata
  // (metadata only — no video data is downloaded) and show it as the demo's duration badge.
  useEffect(() => {
    const probes: HTMLVideoElement[] = [];
    let cancelled = false;
    slides.forEach((slide) => {
      if (!slide.videoUrl) return;
      const probe = document.createElement("video");
      probe.preload = "metadata";
      probe.muted = true;
      probe.onloadedmetadata = () => {
        const label = formatDuration(probe.duration);
        if (label && !cancelled) setDurations((prev) => (prev[slide.id] === label ? prev : { ...prev, [slide.id]: label }));
      };
      probe.src = slide.videoUrl;
      probes.push(probe);
    });
    return () => {
      cancelled = true;
      probes.forEach((probe) => {
        probe.onloadedmetadata = null;
        probe.removeAttribute("src");
        probe.load();
      });
    };
  }, [slides]);

  if (!current) return null;
  const select = (index: number) => setActive((index + slides.length) % slides.length);
  const sideSlides = [...slides.slice(active + 1), ...slides.slice(0, active)];
  const currentDuration = durations[current.id];

  const thumb = (slide: HomeHeroSlide) => (
    <button className={`hero-thumb${slide.id === current.id ? " active" : ""}`} key={slide.id} type="button" onClick={() => select(slides.indexOf(slide))} aria-label={`عرض: ${slide.title}`}>
      {slide.imageUrl ? <Image src={slide.imageUrl} alt="" width={480} height={270} unoptimized /> : <span className="home-thumb-fallback">جسور</span>}
      {slide.videoUrl && <span className="play" aria-hidden="true">▶</span>}
      {slide.videoUrl && durations[slide.id] && <span className="duration-badge">{durations[slide.id]}</span>}
    </button>
  );

  return (
    <section className="section tight" id="heroSliderSection" aria-label="أبرز التغطيات المرئية">
      <div className="wrap">
        <div className="hero-slider">
          <div className="hero-side">{sideSlides.slice(0, 3).map(thumb)}</div>
          <div className="hero-main">
            {current.imageUrl ? <Image src={current.imageUrl} alt={current.title} fill sizes="(max-width: 800px) 100vw, 60vw" unoptimized priority /> : <div className="home-hero-fallback" aria-hidden="true"><span>جسور</span></div>}
            {current.videoUrl ? (
              <>
                {currentDuration && <span className="duration-badge duration-badge-lg">{currentDuration}</span>}
                <span className="playbtn" aria-hidden="true">▶</span>
                <button className="hero-main-link hero-main-button" type="button" aria-label={`تشغيل الفيديو: ${current.title}`} onClick={() => setModalOpen(true)} />
              </>
            ) : (
              <Link className="hero-main-link" href={current.href} aria-label={`اقرأ: ${current.title}`} />
            )}
          </div>
          <div className="hero-side">{sideSlides.slice(3, 6).map(thumb)}</div>
        </div>
        <Link className="hero-below" href={current.href}>
          <span className="hero-below__content">
            {current.videoUrl && <span className="hero-below__play" aria-hidden="true">▶</span>}
            <span className="hero-below__copy">
              <span className="hero-below__eyebrow">{current.videoUrl ? "تغطية مرئية" : "أبرز الأخبار"}</span>
              <h2>{current.title}</h2>
              {current.publishedAtUtc && <span className="foot">{formatArabicDate(current.publishedAtUtc)}</span>}
            </span>
          </span>
        </Link>
        <div className="hero-nav">
          <button className="btn btn-ghost btn-sm" type="button" onClick={() => select(active - 1)}>← السابق</button>
          <div className="hero-dots" aria-label="اختيار التغطية">
            {slides.map((slide, index) => <button key={slide.id} type="button" className={index === active ? "active" : ""} aria-label={`التغطية ${index + 1}`} aria-current={index === active ? "true" : undefined} onClick={() => select(index)}>{index + 1}</button>)}
          </div>
          <button className="btn btn-ghost btn-sm" type="button" onClick={() => select(active + 1)}>التالي →</button>
        </div>
      </div>
      {modalOpen && current.videoUrl && (
        <VideoModal
          item={{
            title: current.title,
            videoUrl: current.videoUrl,
            posterUrl: current.imageUrl,
            href: current.href,
            meta: [current.publishedAtUtc ? formatArabicDate(current.publishedAtUtc) : "", currentDuration ?? ""].filter(Boolean).join(" · "),
          }}
          onClose={closeModal}
        />
      )}
    </section>
  );
}
