"use client";

import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import Link from "next/link";

export interface VideoModalItem {
  title: string;
  videoUrl: string;
  posterUrl?: string | null;
  /** The article this video belongs to ("اقرأ التقرير كاملاً"). */
  href: string;
  /** Line under the player, e.g. "date · duration". */
  meta?: string;
}

/**
 * The Final Demo's cinematic video player (openVideoModal in app.js), same markup and
 * classes (.video-overlay / .video-modal-*): Esc or backdrop click closes, page scroll is
 * locked, the loading spinner shows until the video can play, and the video is unloaded
 * on close so nothing keeps streaming in the background.
 */
export function VideoModal({ item, onClose }: { item: VideoModalItem; onClose: () => void }) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const closeRef = useRef<HTMLButtonElement>(null);
  const onCloseRef = useRef(onClose);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    onCloseRef.current = onClose;
  }, [onClose]);

  useEffect(() => {
    const video = videoRef.current;
    const frame = requestAnimationFrame(() => setOpen(true)); // lets the entrance transition run
    document.body.classList.add("video-modal-locked");
    closeRef.current?.focus();
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") onCloseRef.current();
    };
    window.addEventListener("keydown", onKey);
    video?.play().catch(() => undefined); // autoplay can be blocked; the native controls still work

    return () => {
      cancelAnimationFrame(frame);
      document.body.classList.remove("video-modal-locked");
      window.removeEventListener("keydown", onKey);
      if (video) {
        video.pause();
        video.removeAttribute("src");
        video.load();
      }
    };
  }, []);

  return createPortal(
    <div
      className={`video-overlay${open ? " open" : ""}`}
      onClick={(event) => {
        if (event.target === event.currentTarget) onClose();
      }}
    >
      <div className="video-modal-box" role="dialog" aria-modal="true" aria-label="مشغّل الفيديو">
        <div className="video-modal-head">
          <div className="video-modal-heading">
            <span className="eyebrow">فيديو</span>
            <h3>{item.title}</h3>
          </div>
          <button ref={closeRef} className="video-modal-close" type="button" aria-label="إغلاق" onClick={onClose}>✕</button>
        </div>

        <div className="video-modal-player">
          <div className={`video-modal-loading${loading ? " show" : ""}`}><span className="spinner" /></div>
          <video
            ref={videoRef}
            src={item.videoUrl}
            poster={item.posterUrl ?? undefined}
            controls
            playsInline
            preload="auto"
            controlsList="nodownload"
            onWaiting={() => setLoading(true)}
            onCanPlay={() => setLoading(false)}
            onPlaying={() => setLoading(false)}
            onError={() => setLoading(false)}
          />
        </div>

        <div className="video-modal-foot">
          <span className="video-modal-meta">{item.meta}</span>
          <Link className="btn btn-ghost btn-sm" href={item.href} onClick={onClose}>اقرأ التقرير كاملاً →</Link>
        </div>
      </div>
    </div>,
    document.body,
  );
}
