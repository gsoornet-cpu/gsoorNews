"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { cms, CmsError, type CmsArticleSeo } from "@/lib/cms-client";

export default function ArticleSeoEditorPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const [item, setItem] = useState<CmsArticleSeo | null>(null);
  const [error, setError] = useState("");
  const [saved, setSaved] = useState(false);
  const [busy, setBusy] = useState(false);
  const [roles, setRoles] = useState<string[]>([]);
  const [newCategoryName, setNewCategoryName] = useState("");
  const [newCategorySlug, setNewCategorySlug] = useState("");
  useEffect(() => { void Promise.all([cms.getSeo(id), cms.session()]).then(([seo, session]) => { setItem(seo); setRoles(session.roles ?? []); }).catch((e: unknown) => {
    if (e instanceof CmsError && e.status === 401) router.replace("/cms/login");
    else setError(e instanceof Error ? e.message : "تعذر تحميل إعدادات SEO.");
  }); }, [id, router]);

  async function save() {
    if (!item) return;
    setBusy(true); setError(""); setSaved(false);
    try {
      const updated = await cms.updateSeo(id, {
        slug: item.slug, seoTitle: item.seoTitle, seoDescription: item.seoDescription,
        canonicalUrl: item.canonicalUrl, socialTitle: item.socialTitle,
        socialDescription: item.socialDescription, socialImageUrl: item.socialImageUrl,
        noIndex: item.noIndex, noFollow: item.noFollow,
        primaryCategoryId: item.primaryCategoryId,
        secondaryTags: item.secondaryTags,
        twitterTitle: item.twitterTitle, twitterDescription: item.twitterDescription, twitterImageUrl: item.twitterImageUrl,
      });
      setItem({ ...updated, categories: item.categories }); setSaved(true);
    } catch (e) { setError(e instanceof Error ? e.message : "تعذر حفظ إعدادات SEO."); }
    finally { setBusy(false); }
  }

  async function addCategory() {
    if (!newCategoryName.trim() || !newCategorySlug.trim() || !item) return;
    setBusy(true); setError("");
    try {
      await cms.createCategory({ nameAr: newCategoryName.trim(), slug: newCategorySlug.trim(), displayOrder: item.categories.length + 1 });
      const categories = await cms.listCategories(); setItem({ ...item, categories }); setNewCategoryName(""); setNewCategorySlug("");
    } catch (e) { setError(e instanceof Error ? e.message : "تعذر إضافة التصنيف."); }
    finally { setBusy(false); }
  }

  async function deactivateCategory(categoryId: string) {
    if (!item) return;
    setBusy(true); setError("");
    try {
      await cms.deactivateCategory(categoryId);
      const categories = await cms.listCategories();
      setItem({ ...item, categories, primaryCategoryId: item.primaryCategoryId === categoryId ? null : item.primaryCategoryId });
    } catch (e) { setError(e instanceof Error ? e.message : "تعذر إيقاف التصنيف."); }
    finally { setBusy(false); }
  }

  if (!item) return <div className="cms-card">{error || "جارٍ التحميل…"}</div>;
  const change = (key: keyof CmsArticleSeo, value: string | boolean | string[]) => setItem({ ...item, [key]: value } as CmsArticleSeo);
  const textField = (key: "seoTitle" | "seoDescription" | "slug" | "canonicalUrl" | "socialTitle" | "socialDescription" | "socialImageUrl" | "twitterTitle" | "twitterDescription" | "twitterImageUrl", label: string, max: number, hint?: string) => (
    <div className="cms-field" key={key}><label htmlFor={key}>{label}</label>
      <input id={key} value={item[key] ?? ""} maxLength={max} onChange={e => change(key, e.target.value || null as unknown as string)} />
      <span className="cms-hint">{(item[key] ?? "").length} / {max}{hint ? ` — ${hint}` : ""}</span></div>
  );
  return <main className="wrap cms-card" dir="rtl">
    <header className="cms-head"><div><h1>تحسين ظهور المقال</h1><p>{item.title}</p></div><Link className="cms-btn" href={`/cms/articles/${id}`}>عودة للمقال</Link></header>
    {error && <div className="cms-msg error" role="alert">{error}</div>}{saved && <div className="cms-msg ok" role="status">تم حفظ إعدادات SEO.</div>}
    {textField("seoTitle", "عنوان محركات البحث (50–60 حرفًا؛ اتركه فارغًا لاستخدام عنوان المقال)", 60)}
    {textField("seoDescription", "الوصف (150–160 حرفًا؛ اتركه فارغًا لاستخدام الملخص)", 160)}
    {textField("slug", "الرابط المختصر (عربي أو لاتيني، فريد)", 180)}
    <p className="cms-hint">الرابط الحالي: /article/{id}/{item.slug}</p>
    {textField("twitterTitle", "عنوان Twitter (50–60 حرفًا؛ فارغ = عنوان المشاركة)", 60)}
    {textField("twitterDescription", "وصف Twitter (150–160 حرفًا؛ فارغ = وصف المشاركة)", 160)}
    {textField("twitterImageUrl", "صورة Twitter (رابط HTTP أو HTTPS؛ فارغ = صورة المشاركة)", 2048)}
    <div className="cms-field"><label htmlFor="primary-category">التصنيف الرئيسي</label>
      <select id="primary-category" required value={item.primaryCategoryId ?? ""} onChange={e => change("primaryCategoryId", e.target.value)}>
        <option value="" disabled>اختر تصنيفًا رئيسيًا</option>{item.categories.map(category => <option key={category.id} value={category.id}>{category.nameAr}</option>)}
      </select><span className="cms-hint">يدير رئيس التحرير قائمة التصنيفات.</span></div>
    <div className="cms-field"><label htmlFor="secondary-tags">وسوم ثانوية (حتى 12، افصل بينها بفواصل)</label>
      <input id="secondary-tags" value={item.secondaryTags.join("، ")} onChange={e => change("secondaryTags", e.target.value.split(/[،,]/).map(tag => tag.trim()).filter(Boolean))} />
      <span className="cms-hint">أقسام الديمو التحريرية مثل «أخبار المغتربين» ليست بديلًا عن التصنيف الموضوعي للمقال.</span></div>
    {roles.includes("EditorInChief") && <section className="cms-revision" aria-labelledby="taxonomy-heading"><h2 id="taxonomy-heading">إدارة التصنيفات الرئيسية</h2>
      <div className="cms-field"><label htmlFor="new-category-name">اسم التصنيف</label><input id="new-category-name" maxLength={100} value={newCategoryName} onChange={e => setNewCategoryName(e.target.value)} /></div>
      <div className="cms-field"><label htmlFor="new-category-slug">معرّف التصنيف</label><input id="new-category-slug" maxLength={120} value={newCategorySlug} onChange={e => setNewCategorySlug(e.target.value)} /></div>
      <button className="cms-btn" type="button" disabled={busy || !newCategoryName.trim() || !newCategorySlug.trim()} onClick={() => void addCategory()}>إضافة تصنيف</button>
      <ul>{item.categories.map(category => <li key={category.id}>{category.nameAr} ({category.slug}) <button type="button" className="cms-btn danger" disabled={busy} onClick={() => void deactivateCategory(category.id)}>إيقاف</button></li>)}</ul>
    </section>}
    {textField("canonicalUrl", "الرابط الأساسي (اختياري؛ HTTP أو HTTPS)", 2048)}
    {textField("socialTitle", "عنوان المشاركة (اختياري)", 300)}
    {textField("socialDescription", "وصف المشاركة (اختياري)", 500)}
    {textField("socialImageUrl", "صورة المشاركة (رابط HTTP أو HTTPS)", 2048)}
    <label><input type="checkbox" checked={item.noIndex} onChange={e => change("noIndex", e.target.checked)} /> منع الفهرسة وإخراج المقال من خرائط الموقع</label>
    <label><input type="checkbox" checked={item.noFollow} onChange={e => change("noFollow", e.target.checked)} /> منع تتبع الروابط</label>
    <div className="cms-actions"><button className="cms-btn primary" disabled={busy || item.slug.trim().length === 0 || !item.primaryCategoryId} onClick={() => void save()}>حفظ SEO</button></div>
  </main>;
}
