"use client";

import { useCallback, useEffect, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { cms, cmsAds, CmsError, type CmsAdPlacement } from "@/lib/cms-client";
import { cmsMedia, type CmsMediaAsset } from "@/lib/cms-media-client";

type Surface = "web" | "mobile";
type SlotDefinition = { key: string; label: string; surfaces: Surface[]; ratio: string; note: string };

const SLOTS: SlotDefinition[] = [
  { key: "navbar-banner", label: "بانر الهيدر بين الشعار والبحث", surfaces: ["web"], ratio: "3 / 1", note: "مساحة عرضية في الهيدر، تظهر على الويب فقط بين شعار جسور وزر البحث" },
  { key: "hero-after-videos", label: "بعد فيديوهات المقدمة", surfaces: ["mobile"], ratio: "1 / 1", note: "إعلان مربع بعد الفيديوهات" },
  { key: "after-mughtarib", label: "بعد أخبار المغتربين", surfaces: ["mobile"], ratio: "1 / 1", note: "إعلان مربع" },
  { key: "success-ad", label: "قسم قصص النجاح", surfaces: ["web", "mobile"], ratio: "1 / 1", note: "بجوار القصص على الويب، وفوق الاستطلاع على الهاتف" },
  { key: "atmaen-ad", label: "داخل قسم اطمن", surfaces: ["web", "mobile"], ratio: "16 / 8", note: "داخل مساحة الإعلان الموجودة" },
  { key: "editorial-top", label: "قبل خط أحمر ومع مسؤول", surfaces: ["web"], ratio: "3 / 1", note: "مساحة عرضية جديدة — الويب فقط" },
  { key: "latest-news-square", label: "أسفل أحدث الأخبار", surfaces: ["web"], ratio: "1 / 1", note: "بعد ستة أخبار — الويب فقط" },
  { key: "lamma-top", label: "أعلى قسم اللمة الحلوة", surfaces: ["web"], ratio: "3 / 1", note: "مساحة عرضية جديدة — الويب فقط" },
  { key: "services-inline", label: "بين خدمات الموقع", surfaces: ["web"], ratio: "4 / 3", note: "تظهر كعنصر داخل شريط الخدمات — الويب فقط" },
];

const SURFACE_LABEL: Record<Surface, string> = { web: "الويب", mobile: "الموبايل" };

export default function CmsAdvertisementsPage() {
  const router = useRouter();
  const [assets, setAssets] = useState<CmsMediaAsset[]>([]);
  const [assetPage, setAssetPage] = useState(1);
  const [assetTotal, setAssetTotal] = useState(0);
  const [assetSearch, setAssetSearch] = useState("");
  const [items, setItems] = useState<CmsAdPlacement[]>([]);
  const [activeSurface, setActiveSurface] = useState<Surface>("web");
  const [selectedAssetId, setSelectedAssetId] = useState<string | null>(null);
  const [loaded, setLoaded] = useState(false);
  const [busy, setBusy] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const session = await cms.session();
      if (!session.authenticated) { router.replace("/cms/login"); return; }
      if (!session.roles?.some((role) => ["SeniorEditor", "ManagingEditor", "EditorInChief"].includes(role))) {
        setError("إدارة الإعلانات متاحة لمحرر أول أو مدير تحرير أو رئيس تحرير.");
        return;
      }
      const [placements, media] = await Promise.all([cmsAds.list(), cmsMedia.list(1, 100, "Image")]);
      setItems(placements);
      setAssets(media.items);
      setAssetPage(media.page);
      setAssetTotal(media.totalCount);
      setDirty(false);
      setError(null);
    } catch (e) {
      if (e instanceof CmsError && e.status === 401) { router.replace("/cms/login"); return; }
      setError(e instanceof Error ? e.message : "تعذّر تحميل إعدادات الإعلانات.");
    } finally {
      setLoaded(true);
    }
  }, [router]);

  useEffect(() => { void load(); }, [load]);

  function assign(slot: SlotDefinition, surface: Surface, assetId: string) {
    const asset = assets.find((item) => item.id === assetId);
    if (!asset) return;
    const previous = items.find((item) => item.slotKey === slot.key && item.surface === surface);
    const next: CmsAdPlacement = {
      slotKey: slot.key,
      surface,
      mediaAssetId: asset.id,
      mediaUrl: asset.publicUrl,
      targetUrl: previous?.targetUrl ?? "",
      altText: previous?.altText ?? asset.altText,
      displayOrder: 0,
    };
    setItems((current) => [...current.filter((item) => !(item.slotKey === slot.key && item.surface === surface)), next]);
    setDirty(true);
    setSelectedAssetId(null);
    setNotice(null);
  }

  function update(slotKey: string, surface: Surface, field: "targetUrl" | "altText", value: string) {
    setItems((current) => current.map((item) => item.slotKey === slotKey && item.surface === surface ? { ...item, [field]: value } : item));
    setDirty(true);
    setNotice(null);
  }

  function remove(slotKey: string, surface: Surface) {
    setItems((current) => current.filter((item) => !(item.slotKey === slotKey && item.surface === surface)));
    setDirty(true);
    setNotice(null);
  }

  async function upload(file?: File) {
    if (!file) return;
    setBusy(true); setError(null); setNotice(null);
    try {
      const asset = await cmsMedia.upload(file, {
        altText: file.name.replace(/\.[^.]+$/, "").slice(0, 500) || "إعلان ممول",
        credit: "إعلان ممول", caption: "", focalPointX: null, focalPointY: null,
      });
      setAssets((current) => [asset, ...current.filter((item) => item.id !== asset.id)]);
      setAssetTotal((current) => current + 1);
      setNotice("تم رفع الإعلان إلى مكتبة الصور. اسحبه إلى مساحة الظهور المطلوبة.");
    } catch (e) {
      setError(e instanceof Error ? e.message : "تعذّر رفع الإعلان.");
    } finally {
      setBusy(false);
    }
  }

  async function save() {
    setBusy(true); setError(null); setNotice(null);
    try {
      if (items.some((item) => item.surface === activeSurface && (!item.targetUrl.trim() || !/^https?:\/\//i.test(item.targetUrl.trim())))) {
        setError("أضف رابطًا يبدأ بـ https:// أو http:// لكل إعلان قبل الحفظ.");
        return;
      }
      await cmsAds.save(items.map((item, index) => ({
        slotKey: item.slotKey,
        surface: item.surface,
        mediaAssetId: item.mediaAssetId,
        targetUrl: item.targetUrl.trim(),
        altText: item.altText.trim(),
        displayOrder: index,
      })));
      setNotice(`تم حفظ إعدادات إعلانات ${SURFACE_LABEL[activeSurface]}.`);
      setDirty(false);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "تعذّر حفظ توزيع الإعلانات.");
    } finally {
      setBusy(false);
    }
  }

  async function loadMoreAssets() {
    setBusy(true); setError(null);
    try {
      const nextPage = assetPage + 1;
      const result = await cmsMedia.list(nextPage, 100, "Image");
      setAssets((current) => [...current, ...result.items.filter((asset) => !current.some((existing) => existing.id === asset.id))]);
      setAssetPage(result.page);
    } catch (e) {
      setError(e instanceof Error ? e.message : "تعذّر تحميل صور إضافية.");
    } finally {
      setBusy(false);
    }
  }

  function switchSurface(surface: Surface) {
    if (surface === activeSurface) return;
    if (dirty) {
      setNotice(`احفظ تغييرات ${SURFACE_LABEL[activeSurface]} أولًا، ثم انتقل إلى ${SURFACE_LABEL[surface]}.`);
      return;
    }
    setActiveSurface(surface);
    setSelectedAssetId(null);
    setNotice(null);
  }

  function drop(event: React.DragEvent<HTMLDivElement>, slot: SlotDefinition, surface: Surface) {
    event.preventDefault();
    const assetId = event.dataTransfer.getData("text/plain");
    if (assetId) assign(slot, surface, assetId);
  }

  function renderSlot(slot: SlotDefinition, surface: Surface) {
    const placement = items.find((item) => item.slotKey === slot.key && item.surface === surface);
    return <div className={`ad-drop-slot${placement ? " is-filled" : ""}`} key={`${slot.key}-${surface}`}
      onDragOver={(event) => event.preventDefault()} onDrop={(event) => drop(event, slot, surface)}>
      <div className="ad-drop-slot__head"><strong>{slot.label}</strong><span>{SURFACE_LABEL[surface]}</span></div>
      <small>{slot.note}</small>
      {placement ? <>
        <div className="ad-drop-slot__creative" style={{ aspectRatio: slot.ratio }}>
          <Image src={placement.mediaUrl} alt={placement.altText} fill unoptimized sizes="(max-width: 700px) 90vw, 400px" />
        </div>
        <label>رابط المعلن<input dir="ltr" type="url" inputMode="url" placeholder="https://example.com" value={placement.targetUrl} disabled={busy} onChange={(event) => update(slot.key, surface, "targetUrl", event.target.value)} /></label>
        <label>وصف الصورة<input value={placement.altText} maxLength={500} disabled={busy} onChange={(event) => update(slot.key, surface, "altText", event.target.value)} /></label>
        <button className="cms-btn danger" type="button" disabled={busy} onClick={() => remove(slot.key, surface)}>إزالة من هذه المساحة</button>
        {selectedAssetId && <button className="cms-btn" type="button" disabled={busy} onClick={() => assign(slot, surface, selectedAssetId)}>استبدال بالصورة المحددة</button>}
      </> : <div className="ad-drop-slot__empty">اسحب صورة إعلان إلى هنا</div>}
      {!placement && selectedAssetId && <button className="cms-btn primary" type="button" disabled={busy} onClick={() => assign(slot, surface, selectedAssetId)}>استخدم الصورة المحددة هنا</button>}
    </div>;
  }

  return <main className="cms-card cms-ad-manager" dir="rtl">
    <div className="cms-head"><div><h1>إدارة الإعلانات</h1><p className="cms-hint">ارفع المواد الإعلانية، ثم اسحب كل صورة إلى المساحة المناسبة لكل واجهة. أضف رابط المعلن قبل الحفظ.</p></div><Link className="cms-btn" href="/cms">لوحة الأخبار</Link></div>
    {error && <div className="cms-msg error" role="alert">{error}</div>}
    {notice && <div className="cms-msg success" role="status">{notice}</div>}
    {!loaded ? <p>جارٍ تحميل المساحات…</p> : <section className="cms-ad-workspace" aria-label="إدارة مواضع الإعلانات">
      <div className="cms-ad-stage" dir="rtl">
        <div className="cms-ad-stage__toolbar">
          <div><h2>معاينة المساحات</h2><p>{dirty ? "هناك تغييرات غير محفوظة" : "كل واجهة تُحفظ بشكل مستقل"}</p></div>
          <div className="cms-ad-surface-tabs" role="tablist" aria-label="اختيار الواجهة">
            {(["web", "mobile"] as const).map((surface) => <button key={surface} type="button" role="tab" aria-selected={activeSurface === surface} className={activeSurface === surface ? "active" : ""} onClick={() => switchSurface(surface)}>{SURFACE_LABEL[surface]}</button>)}
          </div>
          <button className="cms-btn primary cms-ad-stage__save" type="button" disabled={busy || !dirty} onClick={() => void save()}>{busy ? "جارٍ الحفظ…" : `حفظ ${SURFACE_LABEL[activeSurface]}`}</button>
        </div>
        <div className={`cms-ad-preview cms-ad-preview--${activeSurface}`}>
          <header><span>معاينة {SURFACE_LABEL[activeSurface]}</span><small>{activeSurface === "mobile" ? "المواضع المخصصة للموبايل والمواضع المشتركة" : "المواضع المخصصة للويب والمواضع المشتركة"}</small></header>
          {SLOTS.filter((slot) => slot.surfaces.includes(activeSurface)).map((slot) => renderSlot(slot, activeSurface))}
        </div>
      </div>

      <aside className="cms-ad-library-pane" dir="rtl">
        <div className="cms-ad-library-head"><div><h2>مكتبة صور الإعلانات</h2><p>اختر صورة ثم ضعها في مساحة، أو اسحبها مباشرة.</p></div>
          <label className="cms-btn primary">{busy ? "جارٍ التحميل…" : "رفع صورة"}<input hidden type="file" accept="image/jpeg,image/png,image/webp,image/avif" disabled={busy} onChange={(event) => { void upload(event.target.files?.[0]); event.currentTarget.value = ""; }} /></label>
          <input className="cms-ad-search" type="search" value={assetSearch} onChange={(event) => setAssetSearch(event.target.value)} placeholder="ابحث في الصور…" aria-label="البحث في صور الإعلانات" />
          <span className="cms-ad-library-count">{assets.length} من {assetTotal} صورة</span>
        </div>
        <div className="cms-ad-assets-scroll">
          <div className="cms-ad-assets">
            {assets.filter((asset) => `${asset.altText} ${asset.fileName}`.toLocaleLowerCase().includes(assetSearch.trim().toLocaleLowerCase())).map((asset) => <button className={`cms-ad-asset${selectedAssetId === asset.id ? " selected" : ""}`} key={asset.id} type="button" draggable onClick={() => setSelectedAssetId((current) => current === asset.id ? null : asset.id)} onDragStart={(event) => event.dataTransfer.setData("text/plain", asset.id)} title="اختر الصورة أو اسحبها إلى مساحة الإعلان" aria-pressed={selectedAssetId === asset.id}>
              <Image src={asset.publicUrl} alt={asset.altText} width={180} height={110} unoptimized />
              <small>{asset.altText || asset.fileName}</small>
              <span>{selectedAssetId === asset.id ? "محددة · اضغط مساحة للإضافة" : "اسحب أو اختر"}</span>
            </button>)}
            {assets.length === 0 && <p className="cms-hint">لا توجد صور في مكتبة الوسائط بعد. ارفع صورة إعلان للبدء.</p>}
            {assets.length > 0 && assets.filter((asset) => `${asset.altText} ${asset.fileName}`.toLocaleLowerCase().includes(assetSearch.trim().toLocaleLowerCase())).length === 0 && <p className="cms-hint">لا توجد صور تطابق البحث.</p>}
          </div>
          {assets.length < assetTotal && <button className="cms-btn cms-ad-load-more" type="button" disabled={busy} onClick={() => void loadMoreAssets()}>{busy ? "جارٍ التحميل…" : `تحميل صور إضافية (${assetTotal - assets.length})`}</button>}
        </div>
      </aside>
    </section>}
  </main>;
}
