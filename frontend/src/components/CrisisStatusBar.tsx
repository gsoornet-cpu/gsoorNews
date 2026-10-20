import Link from "next/link";
import { getPublicCrisisMode } from "@/lib/news-api";

export async function CrisisStatusBar() {
  try {
    const crisis = await getPublicCrisisMode();
    if (!crisis.isActive) return null;
    const severity = crisis.severity === "emergency" ? "طوارئ" : crisis.severity === "advisory" ? "تنبيه" : "عاجل";
    return <aside className={`crisis-bar crisis-bar--${crisis.severity}`} role="status" aria-label="تنبيه أزمة نشطة">
      <div className="crisis-bar__inner"><span className="crisis-bar__badge"><i className="crisis-bar__dot" aria-hidden="true" />{severity}</span>
        <p className="crisis-bar__text"><strong>{crisis.title}</strong><span> — {crisis.summary}</span></p>
        <Link className="crisis-bar__cta" href="/crisis">مركز الأزمة</Link>
      </div>
    </aside>;
  } catch {
    // Crisis information is fail-closed: API unavailability must not show stale or demo alerts.
    return null;
  }
}
