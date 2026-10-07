import Link from "next/link";
import Image from "next/image";
import type { StorySummary } from "@/lib/stories-api";
import { formatArabicDate } from "@/lib/format";

/**
 * Mirrors the demo's cardGrid() output (.grid-card > .thumb + h3 + .foot)
 * almost exactly — see app.js's cardGrid(). Editorial images are rendered
 * when the publisher supplies a social/cover image; other source rows retain
 * a branded placeholder rather than a broken image.
 */
export function StoryGridCard({ story }: { story: StorySummary }) {
  const place = story.cityNameAr ?? story.countryNameAr ?? null;
  const publishedLabel = formatArabicDate(story.publishedAtUtc);

  return (
    <Link href={story.slug ? `/article/${story.id}/${encodeURIComponent(story.slug)}` : `/article/${story.id}`} className="grid-card" aria-label={story.title}>
      <div className={`thumb${story.imageUrl ? "" : " thumb-placeholder"}`} aria-hidden="true">
        {story.imageUrl ? <Image src={story.imageUrl} alt="" width={900} height={600} unoptimized /> : <span>ج</span>}
      </div>

      <h3>{story.title}</h3>

      <div className="foot">
        {place && <span>{place}</span>}
        {place && publishedLabel && <span>·</span>}
        {publishedLabel && <span>{publishedLabel}</span>}
      </div>
    </Link>
  );
}
