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
  const [items, setItems] = useState<CmsAdPlacement[]>([]);
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
      if (items.some((item) => !item.targetUrl.trim() || !/^https?:\/\//i.test(item.targetUrl.trim()))) {
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
      setNotice("تم حفظ الإعلانات وتوزيعها على الويب والموبايل.");
      setDirty(false);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "تعذّر حفظ توزيع الإعلانات.");
    } finally {
      setBusy(false);
    }
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
      </> : <div className="ad-drop-slot__empty">اسحب صورة إعلان إلى هنا</div>}
    </div>;
  }

  return <main className="cms-card cms-ad-manager" dir="rtl">
    <div className="cms-head"><div><h1>إدارة الإعلانات</h1><p className="cms-hint">ارفع المواد الإعلانية، ثم اسحب كل صورة إلى المساحة المناسبة لكل واجهة. أضف رابط المعلن قبل الحفظ.</p></div><Link className="cms-btn" href="/cms">لوحة الأخبار</Link></div>
    {error && <div className="cms-msg error" role="alert">{error}</div>}
    {notice && <div className="cms-msg success" role="status">{notice}</div>}
    <section className="cms-ad-library">
      <div><h2>مكتبة صور الإعلانات</h2><p className="cms-hint">يمكنك استخدام صورة إعلان في أكثر من مساحة. الصور المرفوعة تحفظ في مكتبة الوسائط.</p></div>
      <label className="cms-btn primary">{busy ? "جارٍ الرفع أو الحفظ…" : "رفع صورة إعلان"}<input hidden type="file" accept="image/jpeg,image/png,image/webp,image/avif" disabled={busy} onChange={(event) => { void upload(event.target.files?.[0]); event.currentTarget.value = ""; }} /></label>
      <div className="cms-ad-assets">
        {assets.map((asset) => <div className="cms-ad-asset" key={asset.id} draggable onDragStart={(event) => event.dataTransfer.setData("text/plain", asset.id)} title="اسحب إلى مساحة الإعلان">
          <Image src={asset.publicUrl} alt={asset.altText} width={180} height={110} unoptimized />
          <small>{asset.altText || asset.fileName}</small>
          <span>اسحب للتوزيع</span>
        </div>)}
        {loaded && assets.length === 0 && <p className="cms-hint">لا توجد صور في مكتبة الوسائط بعد. ارفع صورة إعلان للبدء.</p>}
      </div>
    </section>

    {!loaded ? <p>جارٍ تحميل المساحات…</p> : <section className="cms-ad-previews" aria-label="معاينة مواضع الإعلانات">
      <div className="cms-ad-preview cms-ad-preview--web"><header><span>معاينة الويب</span><small>المواضع المخصصة للويب فقط والمواضع المشتركة</small></header>
        {SLOTS.filter((slot) => slot.surfaces.includes("web")).map((slot) => renderSlot(slot, "web"))}
      </div>
      <div className="cms-ad-preview cms-ad-preview--mobile"><header><span>معاينة الموبايل</span><small>واجهة مبسطة بلا أخبار؛ المساحات الإعلانية فقط</small></header>
        {SLOTS.filter((slot) => slot.surfaces.includes("mobile")).map((slot) => renderSlot(slot, "mobile"))}
      </div>
    </section>}
    <div className="cms-actions cms-ad-save"><button className="cms-btn primary" type="button" disabled={busy || !dirty} onClick={() => void save()}>{busy ? "جارٍ الحفظ…" : "حفظ توزيع الإعلانات"}</button>{dirty && <span className="cms-hint">هناك تغييرات غير محفوظة.</span>}</div>
  </main>;
}
