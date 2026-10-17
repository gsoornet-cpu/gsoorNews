import Image from "next/image";
import type { PublicAdPlacement } from "@/lib/news-api";

export function ConfiguredAd({ placement, className = "", ratio = "1 / 1", label = true, naturalSize = false }: {
  placement?: PublicAdPlacement;
  className?: string;
  ratio?: string;
  label?: boolean;
  /** Let the creative keep its source aspect ratio instead of fitting a fixed slot. */
  naturalSize?: boolean;
}) {
  if (!placement) return null;
  return <aside className={`configured-ad ${className}`.trim()} aria-label="إعلان ممول">
    {label && <span className="home-wide-ad__label">مساحة إعلانية <span aria-hidden="true">·</span> إعلان ممول</span>}
    <a href={placement.targetUrl} target="_blank" rel="sponsored noopener noreferrer" aria-label={`${placement.altText} — زيارة موقع المعلن`}>
      {naturalSize
        ? <span className="configured-ad__image configured-ad__image--natural">
            <Image src={placement.mediaUrl} alt={placement.altText} width={1600} height={900} unoptimized sizes="(max-width: 860px) 100vw, 60vw" style={{ width: "100%", height: "auto" }} />
          </span>
        : <span className="configured-ad__image" style={{ aspectRatio: ratio }}>
            <Image src={placement.mediaUrl} alt={placement.altText} fill unoptimized sizes="(max-width: 860px) 100vw, 60vw" />
          </span>}
    </a>
  </aside>;
}
