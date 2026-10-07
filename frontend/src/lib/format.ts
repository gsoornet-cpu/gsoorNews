// Intl-based Arabic-Egyptian date formatting, used anywhere a Story's
// PublishedAtUtc needs to be shown. The demo's own topbar clock hand-rolls
// day/month name arrays for the *current* time — this covers arbitrary
// past dates from real data, which that function was never meant to do.
export function formatArabicDate(iso: string | null): string | null {
  if (!iso) {
    return null;
  }

  return new Intl.DateTimeFormat("ar-EG", {
    day: "numeric",
    month: "long",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).format(new Date(iso));
}

// Simple, honest reading-time estimate (no external library) — 200 Arabic
// words/minute is a commonly cited baseline; rounded up so a very short
// article still reads as "دقيقة واحدة" rather than "0 دقيقة".
// Takes PLAIN TEXT (use htmlToPlainText for an HTML body: tag and attribute
// tokens must not be counted as words).
export function estimateReadMinutes(content: string | null): number {
  if (!content) {
    return 1;
  }

  const words = content.trim().split(/\s+/).filter(Boolean).length;
  return Math.max(1, Math.ceil(words / 200));
}
