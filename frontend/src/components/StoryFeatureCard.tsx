import Link from "next/link";
import Image from "next/image";
import type { StorySummary } from "@/lib/stories-api";
import { formatArabicDate } from "@/lib/format";

/**
 * Mirrors the demo's cardStory() output (.story-card > .thumb + .body).
 * Used for the homepage's single lead story instead of the demo's hero
 * video slider — see SiteHeader/page.tsx comments for why the video slider
 * itself isn't reused: it's built entirely around a video content type
 * (play button, duration badge, video modal) that doesn't exist anywhere
 * in the Phase 1 data model. This still gives the top story real visual
 * prominence using an actually-applicable class from the same design
 * system, rather than forcing video-specific markup onto text content.
 */
export function StoryFeatureCard({ story }: { story: StorySummary }) {
  const place = story.cityNameAr ?? story.countryNameAr ?? null;
  const publishedLabel = formatArabicDate(story.publishedAtUtc);

  return (
    <Link href={story.slug ? `/article/${story.id}/${encodeURIComponent(story.slug)}` : `/article/${story.id}`} className="story-card" aria-label={story.title}>
      <div className={`thumb${story.imageUrl ? "" : " thumb-placeholder"}`} aria-hidden="true">
        {story.imageUrl ? <Image src={story.imageUrl} alt="" width={1200} height={700} unoptimized /> : <span>ج</span>}
      </div>

      <div className="body">
        <h3>{story.title}</h3>

        {story.excerpt && <p style={{ color: "var(--text-mute)", margin: "8px 0" }}>{story.excerpt}</p>}

        <div className="foot">
          {place && <span>{place}</span>}
          {place && publishedLabel && <span>·</span>}
          {publishedLabel && <span>{publishedLabel}</span>}
        </div>
      </div>
    </Link>
  );
}
