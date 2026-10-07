"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import Image from "next/image";
import { useRouter } from "next/navigation";
import { cms, cmsHomeVideos, CmsError, type CmsHomeVideo } from "@/lib/cms-client";
import { formatArabicDate } from "@/lib/format";

const MAX_VIDEOS = 12;

/**
 * Curates the homepage video hero: tick the published videos to show and put them in order.
 * Nothing is added automatically; with no selection the hero is not rendered at all.
 */
export default function HomeVideosPage() {
  const router = useRouter();
  const [selected, setSelected] = useState<CmsHomeVideo[]>([]);
  const [available, setAvailable] = useState<CmsHomeVideo[]>([]);
  const [loaded, setLoaded] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const session = await cms.session();
      if (!session.authenticated) { router.replace("/cms/login"); return; }
      const all = await cmsHomeVideos.list();
      setSelected(all.filter((v) => v.homeVideoOrder !== null).sort((a, b) => (a.homeVideoOrder ?? 0) - (b.homeVideoOrder ?? 0)));
      setAvailable(all.filter((v) => v.homeVideoOrder === null));
      setDirty(false);
      setError(null);
    } catch (e) {
      if (e instanceof CmsError && e.status === 401) { router.replace("/cms/login"); return; }
      setError(e instanceof CmsError && e.status === 403 ? "هذه الصفحة لمحرر أول أو مدير تحرير أو رئيس تحرير فقط." : e instanceof Error ? e.message : "تعذّر تحميل الفيديوهات.");
    } finally {
      setLoaded(true);
    }
  }, [router]);

  useEffect(() => { void load(); }, [load]);

  const add = (video: CmsHomeVideo) => {
    if (selected.length >= MAX_VIDEOS) { setError(`الحد الأقصى ${MAX_VIDEOS} فيديو في الصفحة الرئيسية.`); return; }
    setError(null); setNotice(null); setDirty(true);
    setSelected((list) => [...list, video]);
    setAvailable((list) => list.filter((v) => v.articleId !== video.articleId));
  };
  const remove = (video: CmsHomeVideo) => {
    setNotice(null); setDirty(true);
    setSelected((list) => list.filter((v) => v.articleId !== video.articleId));
    setAvailable((list) => [...list, video].sort((a, b) => b.publishedAtUtc.localeCompare(a.publishedAtUtc)));
  };
  const move = (index: number, delta: -1 | 1) => {
    const target = index + delta;
    if (target < 0 || target >= selected.length) return;
    setNotice(null); setDirty(true);
    setSelected((list) => { const next = [...list]; [next[index], next[target]] = [next[target], next[index]]; return next; });
  };

  async function save() {
    setBusy(true); setError(null); setNotice(null);
    try {
      await cmsHomeVideos.save(selected.map((v) => v.articleId));
      setNotice(selected.length ? "تم الحفظ. الفيديوهات تظهر الآن في الصفحة الرئيسية بهذا الترتيب." : "تم الحفظ. لا توجد فيديوهات مختارة، فقسم الفيديو مخفي من الصفحة الرئيسية.");
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "تعذّر الحفظ.");
    } finally {
      setBusy(false);
    }
  }

  const row = (video: CmsHomeVideo, children: React.ReactNode, position?: number) => (
    <li key={video.articleId} style={{ display: "flex", alignItems: "center", gap: 12, padding: "10px 0", borderBottom: "1px solid var(--line, #e4e7ec)" }}>
      {position !== undefined && <strong style={{ minWidth: 24 }}>{position}</strong>}
      {video.thumbnailUrl
        ? <Image src={video.thumbnailUrl} alt="" width={96} height={54} unoptimized style={{ objectFit: "cover", borderRadius: 6, flex: "none" }} />
        : <span aria-hidden="true" style={{ width: 96, height: 54, borderRadius: 6, background: "#101c30", color: "#fff", display: "grid", placeItems: "center", flex: "none", fontWeight: 800 }}>جسور</span>}
      <span style={{ flex: 1, minWidth: 0 }}>
        <span style={{ display: "block", fontWeight: 700 }}>{video.title}</span>
        <small className="cms-hint">{formatArabicDate(video.publishedAtUtc)}{!video.thumbnailUrl && " · بدون صورة غلاف (ستظهر «جسور» مكانها)"}</small>
      </span>
      <span className="cms-actions" style={{ flex: "none" }}>{children}</span>
    </li>
  );

  return (
    <main className="cms-card" dir="rtl">
      <div className="cms-head"><h1>فيديوهات الصفحة الرئيسية</h1><Link className="cms-btn" href="/cms">لوحة الأخبار</Link></div>
      <p className="cms-hint">اختر الفيديوهات التي تظهر في أول الصفحة الرئيسية ورتّبها. لا يُضاف شيء تلقائيًا، ولو لم تختر أي فيديو يختفي هذا القسم من الموقع. تظهر هنا فقط الأخبار المنشورة التي لها فيديو جاهز.</p>
      {error && <div className="cms-msg error" role="alert">{error}</div>}
      {notice && <div className="cms-msg success" role="status">{notice}</div>}
      {!loaded ? <p>جارٍ التحميل…</p> : <>
        <h2>المعروضة في الصفحة الرئيسية ({selected.length}/{MAX_VIDEOS})</h2>
        {selected.length === 0 ? <p className="cms-hint">لا توجد فيديوهات مختارة — القسم مخفي حاليًا.</p> : (
          <ol style={{ listStyle: "none", padding: 0, margin: 0 }}>
            {selected.map((video, index) => row(video, <>
              <button type="button" className="cms-btn" disabled={busy || index === 0} onClick={() => move(index, -1)} aria-label="تقديم">↑</button>
              <button type="button" className="cms-btn" disabled={busy || index === selected.length - 1} onClick={() => move(index, 1)} aria-label="تأخير">↓</button>
              <button type="button" className="cms-btn" disabled={busy} onClick={() => remove(video)}>إزالة</button>
            </>, index + 1))}
          </ol>
        )}

        <h2 style={{ marginTop: 28 }}>فيديوهات منشورة متاحة للإضافة ({available.length})</h2>
        {available.length === 0 ? <p className="cms-hint">لا توجد فيديوهات أخرى. انشر خبرًا بفيديو (اختر «عرض فيديو من هذا الخبر في قسم الفيديوهات») ليظهر هنا.</p> : (
          <ul style={{ listStyle: "none", padding: 0, margin: 0 }}>
            {available.map((video) => row(video, <button type="button" className="cms-btn" disabled={busy} onClick={() => add(video)}>إضافة للصفحة الرئيسية</button>))}
          </ul>
        )}

        <div className="cms-actions" style={{ marginTop: 24 }}>
          <button type="button" className="cms-btn primary" disabled={busy || !dirty} onClick={() => void save()}>{busy ? "جارٍ الحفظ…" : "حفظ الترتيب"}</button>
          {dirty && <span className="cms-hint">لديك تغييرات غير محفوظة.</span>}
        </div>
      </>}
    </main>
  );
}
