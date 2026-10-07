"use client";

import Image from "next/image";
import Link from "next/link";
import { useEffect, useState } from "react";
import type { PublishedVideo } from "@/lib/news-api";
import { formatArabicDate } from "@/lib/format";
import { VideoModal, type VideoModalItem } from "./VideoModal";

function formatDuration(seconds: number): string {
  if (!Number.isFinite(seconds) || seconds <= 0) return "";
  const whole = Math.floor(seconds);
  return `${Math.floor(whole / 60)}:${String(whole % 60).padStart(2, "0")}`;
}

/** Homepage video feature using the featured-player treatment from the approved Final Demo. */
export function HomeVideoFeature({ videos }: { videos: PublishedVideo[] }) {
  const [index, setIndex] = useState(0);
  const [duration, setDuration] = useState("");
  const [playing, setPlaying] = useState(false);
  const video = videos[index];

  useEffect(() => {
    if (!video) return;
    setDuration("");
    const probe = document.createElement("video");
    probe.preload = "metadata";
    probe.onloadedmetadata = () => setDuration(formatDuration(probe.duration));
    probe.src = video.videoUrl;
    return () => {
      probe.removeAttribute("src");
      probe.load();
    };
  }, [video?.id, video?.videoUrl]);

  if (!video) return null;

  const href = `/article/${video.id}/${encodeURIComponent(video.slug)}`;
  const modalItem: VideoModalItem = {
    title: video.title,
    videoUrl: video.videoUrl,
    posterUrl: video.thumbnailUrl,
    href,
    meta: [formatArabicDate(video.publishedAtUtc), video.credit].filter(Boolean).join(" · "),
  };

  return (
    <>
      <section className="section tight on-surface home-video-section" aria-labelledby="home-videos-heading">
        <div className="wrap">
          <div className="section-head">
            <h2 id="home-videos-heading"><Link className="eyebrow" href="/videos">جسور فيديو</Link></h2>
            <Link href="/videos" className="video-rail-cta">شاهد كل الفيديوهات ←</Link>
          </div>
          <article className="video-featured home-video-featured">
            <div className="video-featured-media">
              {video.thumbnailUrl ? (
                <Image src={video.thumbnailUrl} alt={video.title} fill sizes="(max-width: 760px) 100vw, 900px" unoptimized priority={index === 0} />
              ) : <div className="home-hero-fallback" aria-hidden="true"><span>جسور</span></div>}
              {duration && <span className="duration-badge duration-badge-lg">{duration}</span>}
              <button className="playbtn" type="button" aria-label={`تشغيل الفيديو: ${video.title}`} onClick={() => setPlaying(true)}>▶</button>
              {videos.length > 1 && <button
                className="home-video-next"
                type="button"
                aria-label="الفيديو التالي"
                onClick={() => setIndex((current) => (current + 1) % videos.length)}
              >←</button>}
            </div>
            <div className="video-featured-body">
              <span className="eyebrow">{video.caption || "تغطية مرئية"}</span>
              <h2><Link href={href}>{video.title}</Link></h2>
              <time className="home-video-date" dateTime={video.publishedAtUtc}>{formatArabicDate(video.publishedAtUtc)}</time>
            </div>
          </article>
        </div>
      </section>
      {playing && <VideoModal item={modalItem} onClose={() => setPlaying(false)} />}
    </>
  );
}
