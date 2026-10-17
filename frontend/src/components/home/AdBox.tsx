"use client";

import { useEffect, useRef, useState } from "react";
import Image from "next/image";
import type { PublicAdPlacement } from "@/lib/news-api";

/**
 * A standalone sponsored box (own frame, own entrance animation).
 *
 * - `ratio`  : fixed aspect ratio for the creative slot (e.g. "1 / 1").
 * - `fill`   : the box takes whatever height its column has left over (used in the
 *              Lamma row so both columns end on the same line). The creative keeps its
 *              natural size at full width; spare height is filled by the blurred backdrop.
 *
 * The creative is always rendered with `object-fit: contain` on top of a blurred
 * copy of itself, so the original file is never cropped, stretched or re-encoded
 * (`unoptimized`) — any spare space is filled by the blurred backdrop.
 *
 * The entrance (fade + rise, one soft sheen) plays once when the box scrolls into
 * view. The hidden "pending" state only applies where scripting is enabled and is
 * skipped for reduced-motion users, so the ad can never stay invisible.
 */
export function AdBox({ placement, className = "", ratio = "1 / 1", fill = false }: {
  placement?: PublicAdPlacement;
  className?: string;
  ratio?: string;
  fill?: boolean;
}) {
  const ref = useRef<HTMLElement>(null);
  const [seen, setSeen] = useState(false);

  useEffect(() => {
    const node = ref.current;
    if (!node) return;
    const reduced = typeof window.matchMedia === "function" && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (reduced || typeof IntersectionObserver === "undefined") { setSeen(true); return; }
    const observer = new IntersectionObserver((entries) => {
      if (entries.some((entry) => entry.isIntersecting)) { setSeen(true); observer.disconnect(); }
    }, { threshold: 0.2 });
    observer.observe(node);
    return () => observer.disconnect();
  }, []);

  if (!placement) return null;

  const [ratioW, ratioH] = ratio.split("/").map((part) => Number(part.trim()));
  const flowHeight = ratioW > 0 && ratioH > 0 ? Math.round((1600 * ratioH) / ratioW) : 900;
  const classes = ["ad-box", fill ? "ad-box--fill" : "", seen ? "ad-box--in" : "ad-box--pending", className].filter(Boolean).join(" ");
  return (
    <aside ref={ref} className={classes} aria-label="إعلان ممول">
      <span className="ad-box__label">مساحة إعلانية <span aria-hidden="true">·</span> إعلان ممول</span>
      <a className="ad-box__link" href={placement.targetUrl} target="_blank" rel="sponsored noopener noreferrer" aria-label={`${placement.altText} — زيارة موقع المعلن`}>
        <span className="ad-box__media" style={fill ? undefined : { aspectRatio: ratio }}>
          <Image className="ad-box__bg" src={placement.mediaUrl} alt="" aria-hidden="true" fill sizes="64px" unoptimized />
          {fill
            ? <Image className="ad-box__img ad-box__img--flow" src={placement.mediaUrl} alt={placement.altText} width={1600} height={flowHeight} sizes="(max-width: 860px) 100vw, 860px" style={{ width: "100%", height: "auto" }} unoptimized />
            : <Image className="ad-box__img" src={placement.mediaUrl} alt={placement.altText} fill sizes="(max-width: 860px) 100vw, 360px" unoptimized />}
          <span className="ad-box__sheen" aria-hidden="true" />
        </span>
      </a>
    </aside>
  );
}
