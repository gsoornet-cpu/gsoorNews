import { formatArabicDate } from "@/lib/format";
import type { NewsCorrection } from "@/lib/news-api";

const KIND_LABEL: Record<NewsCorrection["kind"], string> = {
  Correction: "تصحيح",
  Notice: "تنويه",
  Clarification: "توضيح",
  EditorNote: "تنويه المحرر",
  Update: "تحديث",
};

/**
 * Public "تنويه صحفي / تصحيح" box (spec §16 trust signals, decision D4).
 * Major corrections render above the article text, the rest below it. The note
 * is plain text rendered as a React text node (no HTML), and the box renders
 * nothing when there is nothing to show.
 */
export function CorrectionsBox({ corrections, major = false }: { corrections: NewsCorrection[]; major?: boolean }) {
  if (corrections.length === 0) {
    return null;
  }

  return (
    <aside className={`article-corrections${major ? " is-major" : ""}`} aria-label="تنويه صحفي / تصحيح">
      <h2>تنويه صحفي / تصحيح</h2>
      <ul>
        {corrections.map((c, i) => (
          <li key={`${c.issuedAtUtc}-${i}`}>
            <span className="correction-kind">{KIND_LABEL[c.kind] ?? c.kind}</span> <time dateTime={c.issuedAtUtc}>{formatArabicDate(c.issuedAtUtc)}</time>
            <p>{c.note}</p>
          </li>
        ))}
      </ul>
    </aside>
  );
}
