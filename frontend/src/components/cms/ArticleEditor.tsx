"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import Image from "next/image";
import { useRouter } from "next/navigation";
import { ArticleHistory } from "@/components/cms/ArticleHistory";
import { BODY_HTML_MAX, RichTextEditor } from "@/components/cms/RichTextEditor";
import { cms, workflowHints, STATUS_META, CmsError, PRESENTATION_DESKS, type CmsArticle, type WorkflowAction } from "@/lib/cms-client";
import { cmsMedia, type CmsMediaAsset } from "@/lib/cms-media-client";

const TITLE_MAX = 300;
const SUMMARY_MAX = 500;
const localDateTimeInput = (date: Date) => {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 16);
};
const videoMediaIds = (html: string) => Array.from(
  html.matchAll(/<video\b[^>]*\bdata-media-id="([0-9a-f-]{36})"/gi),
  (match) => match[1],
);

export function ArticleEditor({ articleId }: { articleId: string | null }) {
  const router = useRouter();
  const [article, setArticle] = useState<CmsArticle | null>(null);
  const [roles, setRoles] = useState<string[] | undefined>(undefined);
  const [revisionOpen, setRevisionOpen] = useState(false);
  const [postPublicationAction, setPostPublicationAction] = useState<"retract" | "archive" | "restoreArchive" | "reinstate" | null>(null);
  const [breakGlassAction, setBreakGlassAction] = useState<"publish" | "unpublish" | null>(null);
  const [breakGlassReason, setBreakGlassReason] = useState("");
  const [scheduleOpen, setScheduleOpen] = useState(false);
  const [scheduleAt, setScheduleAt] = useState(() => localDateTimeInput(new Date(Date.now() + 10 * 60_000)));
  const [scheduleReason, setScheduleReason] = useState("");
  const [publicNotice, setPublicNotice] = useState("");
  const [transitionReason, setTransitionReason] = useState("");
  const [reason, setReason] = useState("");
  const [ready, setReady] = useState(articleId === null);
  const [title, setTitle] = useState("");
  const [authorName, setAuthorName] = useState("");
  const [summary, setSummary] = useState("");
  const [body, setBody] = useState("");
  const [presentationDesks, setPresentationDesks] = useState<string[]>([]);
  const [featuredVideoMediaAssetId, setFeaturedVideoMediaAssetId] = useState("");
  const [coverImageUrl, setCoverImageUrl] = useState("");
  const [coverPickerOpen, setCoverPickerOpen] = useState(false);
  const [coverAssets, setCoverAssets] = useState<CmsMediaAsset[]>([]);
  const [coverLoading, setCoverLoading] = useState(false);
  const [coverUploadBusy, setCoverUploadBusy] = useState(false);
  const [coverUploadProgress, setCoverUploadProgress] = useState<number | null>(null);
  const [coverAltText, setCoverAltText] = useState("");
  const [coverCredit, setCoverCredit] = useState("");
  const [editReason, setEditReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  function fill(a: CmsArticle) {
    setArticle(a);
    setTitle(a.title);
    setAuthorName(a.authorName ?? "");
    setSummary(a.summary ?? "");
    setBody(a.body);
    setPresentationDesks(a.presentationDesks ?? []);
    setFeaturedVideoMediaAssetId(a.featuredVideoMediaAssetId ?? "");
    setCoverImageUrl(a.socialImageUrl ?? "");
    setEditReason("");
  }

  useEffect(() => {
    void (async () => {
      const session = await cms.session();
      if (!session.authenticated) {
        router.replace("/cms/login");
        return;
      }
      setRoles(session.roles);

      if (articleId) {
        try {
          fill(await cms.get(articleId));
        } catch (e) {
          if (e instanceof CmsError && e.status === 401) {
            router.replace("/cms/login");
            return;
          }
          setError(e instanceof CmsError ? e.message : "تعذّر تحميل المقال.");
        }
      }
      setReady(true);
    })();
  }, [articleId, router]);

  async function run(action: () => Promise<void>) {
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      await action();
    } catch (e) {
      if (e instanceof CmsError && e.status === 401) {
        router.replace("/cms/login");
        return;
      }
      setError(e instanceof CmsError ? e.message : "حدث خطأ غير متوقع.");
    } finally {
      setBusy(false);
    }
  }

  const input = () => ({
    title: title.trim(),
    authorName: authorName.trim() || null,
    summary: summary.trim() || null,
    body: body.trim(),
    // Country/city pickers arrive with the geography admin API; the API accepts null.
    countryId: article?.countryId ?? null,
    cityId: article?.cityId ?? null,
    // Decision D4: recorded with the revision, only meaningful while the article is live.
    editReason: article?.status === "Published" ? editReason.trim() || null : null,
    presentationDesks,
    featuredVideoMediaAssetId: featuredVideoMediaAssetId || null,
    // Saved together with the article so the cover can never be lost to a separate (SEO) call.
    coverImageUrl: coverImageUrl.trim(),
  });

  function updateBody(nextBody: string) {
    const ids = videoMediaIds(nextBody);
    setBody(nextBody);
    setFeaturedVideoMediaAssetId((current) => current && ids.includes(current) ? current : ids[0] ?? "");
  }

  const save = () =>
    run(async () => {
      const saved: CmsArticle = article
        ? await cms.update(article.id, input())
        : await cms.create(input());
      // fill() re-reads the cover from what the server actually stored, so the preview
      // can never show a cover that was not really saved.
      fill(saved);
      if (!article) router.replace(`/cms/articles/${saved.id}`);
      else setNotice("تم حفظ التعديلات.");
    });

  async function openCoverPicker() {
    setCoverPickerOpen(true);
    setCoverLoading(true);
    try {
      setCoverAssets((await cmsMedia.list()).items.filter((asset) => asset.kind === "Image"));
    } catch (e) {
      setError(e instanceof Error ? e.message : "تعذّر تحميل مكتبة الصور.");
    } finally {
      setCoverLoading(false);
    }
  }

  async function uploadCover(file: File | undefined) {
    if (!file) return;
    if (!file.type.startsWith("image/")) {
      setError("اختر ملف صورة لاستخدامه كغلاف.");
      return;
    }
    if (!coverAltText.trim() || !coverCredit.trim()) {
      setError("اكتب وصفًا بديلًا للصورة واسم المصدر/المصور قبل الرفع.");
      return;
    }
    setCoverUploadBusy(true);
    setError(null);
    setCoverUploadProgress(0);
    try {
      const asset = await cmsMedia.upload(file, {
        altText: coverAltText.trim(), credit: coverCredit.trim(), caption: "",
        focalPointX: null, focalPointY: null,
      }, setCoverUploadProgress);
      setCoverAssets((current) => [asset, ...current.filter((item) => item.id !== asset.id)]);
      setCoverImageUrl(asset.publicUrl);
      setCoverPickerOpen(false);
    } catch (e) {
      setError(e instanceof Error ? e.message : "تعذّر رفع صورة الغلاف.");
    } finally {
      setCoverUploadBusy(false);
      setCoverUploadProgress(null);
    }
  }

  const submit = () =>
    run(async () => {
      if (!article) return;
      fill(await cms.submit(article.id));
      setNotice("أُرسل المقال للمراجعة التحريرية. لا يمكن تعديله من كاتبه حتى تتم مراجعته.");
    });

  const approve = () =>
    run(async () => {
      if (!article) return;
      fill(await cms.approve(article.id));
      setNotice("تم اعتماد المقال. بإمكان المحرر المسؤول نشره الآن.");
    });

  const requestRevision = () =>
    run(async () => {
      if (!article || !reason.trim()) return;
      fill(await cms.requestRevision(article.id, reason.trim()));
      setReason("");
      setRevisionOpen(false);
      setNotice("أُعيد المقال إلى كاتبه كمسودة مع سبب الإعادة.");
    });

  const publish = (reason?: string) =>
    run(async () => {
      if (!article) return;
      fill(await cms.publish(article.id, reason));
      setNotice("تم نشر المقال ويظهر الآن على الموقع.");
    });

  const schedulePublication = () =>
    run(async () => {
      if (!article || !scheduleAt) return;
      const when = new Date(scheduleAt);
      if (Number.isNaN(when.getTime()) || when.getTime() <= Date.now()) {
        setError("اختر وقتًا مستقبليًا للنشر.");
        return;
      }
      fill(await cms.schedule(article.id, when.toISOString(), scheduleReason.trim() || undefined));
      setScheduleOpen(false);
      setScheduleReason("");
      setNotice("تمت جدولة المقال. سيُنشر تلقائيًا في الموعد المحدد بتوقيتك المحلي.");
    });

  const cancelSchedule = () =>
    run(async () => {
      if (!article) return;
      fill(await cms.cancelSchedule(article.id));
      setNotice("أُلغيت الجدولة وعاد المقال إلى حالة «معتمد».");
    });

  const unpublish = (reason?: string) =>
    run(async () => {
      if (!article) return;
      fill(await cms.unpublish(article.id, reason));
      setNotice("تم إلغاء النشر. عاد المقال إلى المسودات.");
    });

  const runPostPublicationAction = () =>
    run(async () => {
      if (!article || !postPublicationAction) return;
      const result = postPublicationAction === "retract"
        ? await cms.retract(article.id, publicNotice.trim(), transitionReason.trim())
        : postPublicationAction === "archive"
          ? await cms.archive(article.id, transitionReason.trim())
          : postPublicationAction === "restoreArchive"
            ? await cms.restoreArchive(article.id, transitionReason.trim())
            : await cms.reinstate(article.id, transitionReason.trim());
      fill(result);
      setPostPublicationAction(null);
      setPublicNotice("");
      setTransitionReason("");
      setNotice("تم تحديث حالة المقال وتسجيل الإجراء في السجل التحريري.");
    });

  if (!ready) {
    return <p>جارٍ التحميل…</p>;
  }

  const isPublished = article?.status === "Published";
  const canEditContent = !article || (article.status !== "Archived" && article.status !== "Retracted" && article.status !== "Scheduled");
  const missing = !title.trim() || !body.trim() || body.length > BODY_HTML_MAX;
  const canSave = canEditContent && !missing && (!isPublished || editReason.trim().length >= 5);
  const hints: WorkflowAction[] = article ? workflowHints(roles, article.status) : [];
  const meta = article ? STATUS_META[article.status] : null;

  return (
    <div className="cms-card">
      <div className="cms-head">
        <h1>{article ? "تحرير مقال" : "مقال جديد"}</h1>
        <div className="cms-actions">
          {article && meta && <span className={`cms-badge ${meta.badge}`}>{meta.label}</span>}
          {article && isPublished && (
            <Link className="cms-btn" href={`/article/${article.id}/${encodeURIComponent(article.slug ?? "")}`} target="_blank">
              عرض على الموقع
            </Link>
          )}
          {article && roles?.some((role) => ["SeoEditor", "SeniorEditor", "ManagingEditor", "EditorInChief"].includes(role)) && (
            <Link className="cms-btn" href={`/cms/seo/articles/${article.id}`}>إعدادات SEO</Link>
          )}
          <Link className="cms-btn" href="/cms">
            العودة للقائمة
          </Link>
        </div>
      </div>

      {error && (
        <div className="cms-msg error" role="alert">
          {error}
        </div>
      )}
      {notice && (
        <div className="cms-msg ok" role="status">
          {notice}
        </div>
      )}

      <div className="cms-field">
        <label htmlFor="title">العنوان</label>
        <input id="title" value={title} maxLength={TITLE_MAX} disabled={!canEditContent || busy} onChange={(e) => setTitle(e.target.value)} />
        <span className="cms-hint">
          {title.length} / {TITLE_MAX}
        </span>
      </div>

      <div className="cms-field">
        <label htmlFor="author-name">اسم الكاتب (اختياري)</label>
        <input id="author-name" value={authorName} maxLength={120} disabled={!canEditContent || busy} onChange={(event) => setAuthorName(event.target.value)} placeholder="اتركه فارغًا لعرض هيئة تحرير جسور" />
      </div>

      <div className="cms-field">
        <label htmlFor="summary">ملخص قصير (اختياري)</label>
        <textarea id="summary" rows={3} value={summary} maxLength={SUMMARY_MAX} disabled={!canEditContent || busy} onChange={(e) => setSummary(e.target.value)} />
        <span className="cms-hint">
          {summary.length} / {SUMMARY_MAX}
        </span>
      </div>

      <fieldset className="cms-field" disabled={!canEditContent || isPublished || busy}>
        <legend>أماكن ظهور الخبر</legend>
        <p className="cms-hint">اختر مكتبًا أو أكثر قبل النشر. هذا الاختيار مستقل عن التصنيف الموضوعي ووسوم SEO.</p>
        <div className="cms-desk-options">
          {PRESENTATION_DESKS.map((desk) => (
            <label key={desk.slug}>
              <input
                type="checkbox"
                checked={presentationDesks.includes(desk.slug)}
                onChange={(event) => setPresentationDesks((current) => event.target.checked
                  ? [...current, desk.slug]
                  : current.filter((slug) => slug !== desk.slug))}
              />
              {desk.label}
            </label>
          ))}
        </div>
      </fieldset>

      {roles?.some((role) => ["SeoEditor", "SeniorEditor", "ManagingEditor", "EditorInChief"].includes(role)) && (
        <section className="cms-field" aria-labelledby="cover-image-label">
          <label id="cover-image-label">صورة غلاف الخبر</label>
          <p className="cms-hint">هذه صورة مستقلة تظهر أعلى صفحة الخبر وفي بطاقات الموقع. اختيارها لا يضيفها إلى نص الخبر.</p>
          {coverImageUrl && <Image src={coverImageUrl} alt="معاينة غلاف الخبر" width={960} height={480} unoptimized style={{ display: "block", width: "min(100%, 520px)", height: "auto", maxHeight: 260, objectFit: "cover", borderRadius: 10, marginBlock: 10 }} />}
          <div className="cms-actions">
            <button className="cms-btn" type="button" disabled={!canEditContent || busy} onClick={() => void openCoverPicker()}>اختيار من مكتبة الوسائط</button>
            {coverImageUrl && <button className="cms-btn" type="button" disabled={!canEditContent || busy} onClick={() => setCoverImageUrl("")}>إزالة صورة الغلاف</button>}
          </div>
          {coverPickerOpen && <div className="cms-revision" role="dialog" aria-modal="true" aria-label="اختيار صورة الغلاف">
            <h2>اختر صورة غلاف</h2>
            <div className="cms-field">
              <label htmlFor="cover-alt">الوصف البديل</label>
              <input id="cover-alt" value={coverAltText} disabled={coverUploadBusy || busy} onChange={(event) => setCoverAltText(event.target.value)} />
              <label htmlFor="cover-credit">المصدر / المصور</label>
              <input id="cover-credit" value={coverCredit} disabled={coverUploadBusy || busy} onChange={(event) => setCoverCredit(event.target.value)} />
              <label htmlFor="cover-file">رفع صورة غلاف جديدة</label>
              <input id="cover-file" type="file" accept="image/*" disabled={coverUploadBusy || busy} onChange={(event) => { void uploadCover(event.target.files?.[0]); event.currentTarget.value = ""; }} />
              {coverUploadProgress !== null && <progress max={100} value={coverUploadProgress} aria-label="تقدم رفع صورة الغلاف" />}
            </div>
            {coverUploadBusy ? <p role="status">جارٍ رفع الغلاف…</p> : null}
            {coverLoading ? <p>جارٍ تحميل الصور…</p> : coverAssets.length ? <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill,minmax(150px,1fr))", gap: 12 }}>
              {coverAssets.map((asset) => <button key={asset.id} type="button" className="cms-btn" onClick={() => { setCoverImageUrl(asset.publicUrl); setCoverPickerOpen(false); }} style={{ padding: 8 }}>
                <Image src={asset.publicUrl} alt={asset.altText || asset.fileName} width={300} height={220} unoptimized style={{ width: "100%", height: 110, objectFit: "cover", borderRadius: 6 }} />
                <span>{asset.caption || asset.fileName}</span>
              </button>)}
            </div> : <p>لا توجد صور في المكتبة. ارفع الصورة أولًا من زر «إدراج وسائط» داخل المحرر، ثم اخترها هنا كغلاف.</p>}
            <div className="cms-actions"><button className="cms-btn" type="button" disabled={coverUploadBusy} onClick={() => setCoverPickerOpen(false)}>إغلاق</button></div>
          </div>}
        </section>
      )}

      <div className="cms-field">
        <label id="body-label" htmlFor="body">
          نص الخبر
        </label>
        <RichTextEditor id="body" value={body} onChange={updateBody} disabled={busy || !canEditContent} />
        <span className="cms-hint">يُنظَّف التنسيق على الخادم عند الحفظ: لا يُحفظ إلا العناوين والقوائم والاقتباس والروابط والتنسيق الأساسي.</span>
      </div>

      <div className="cms-field">
        <label htmlFor="featured-video">عرض فيديو من هذا الخبر في قسم الفيديوهات</label>
        <select id="featured-video" value={featuredVideoMediaAssetId} disabled={!canEditContent || isPublished || busy || videoMediaIds(body).length === 0}
          onChange={(event) => setFeaturedVideoMediaAssetId(event.target.value)}>
          <option value="">عدم إدراجه في قسم الفيديوهات</option>
          {videoMediaIds(body).map((id, index) => <option key={id} value={id}>الفيديو المضمّن {index + 1}</option>)}
        </select>
        <span className="cms-hint">أدرج ملف فيديو جاهزًا من مكتبة الوسائط داخل الخبر أولًا، ثم اختره هنا. الاختيار ينشر معاينة مستقلة في /videos بعد اعتماد الخبر ونشره.</span>
      </div>

      {isPublished && (
        <div className="cms-field">
        <label htmlFor="edit-reason">سبب التعديل (إلزامي، 5–1000 أحرف)</label>
          <input id="edit-reason" value={editReason} maxLength={1000} onChange={(e) => setEditReason(e.target.value)} />
          <span className="cms-hint">
            المقال منشور: اكتب سبباً لكل تعديل. تُحفَظ النسخة السابقة في السجل، ولتنبيه القراء أضف تنويهاً أو تصحيحاً.
          </span>
        </div>
      )}

      {revisionOpen && (
        <div className="cms-revision">
          <label htmlFor="reason">سبب الإعادة إلى الكاتب (إلزامي — يظهر في سجل المقال)</label>
          <textarea id="reason" rows={3} value={reason} maxLength={1000} onChange={(e) => setReason(e.target.value)} />
          <div className="cms-actions">
            <button className="cms-btn danger" type="button" disabled={busy || !reason.trim()} onClick={requestRevision}>
              تأكيد الإعادة
            </button>
            <button className="cms-btn" type="button" disabled={busy} onClick={() => setRevisionOpen(false)}>
              إلغاء
            </button>
          </div>
        </div>
      )}

      <div className="cms-actions">
        {canEditContent && <button className="cms-btn primary" type="button" disabled={busy || !canSave} onClick={save}>
          {article ? "حفظ التعديلات" : "حفظ كمسودة"}
        </button>}
        {hints.includes("submit") && (
          <button className="cms-btn" type="button" disabled={busy} onClick={submit}>
            إرسال للمراجعة
          </button>
        )}
        {hints.includes("requestRevision") && (
          <button className="cms-btn" type="button" disabled={busy} onClick={() => setRevisionOpen((open) => !open)}>
            إعادة للكاتب
          </button>
        )}
        {hints.includes("approve") && (
          <button className="cms-btn success" type="button" disabled={busy} onClick={approve}>
            اعتماد
          </button>
        )}
        {hints.includes("publish") && (
          <button className="cms-btn success" type="button" disabled={busy} onClick={() =>
            roles?.includes("SystemAdmin") ? setBreakGlassAction("publish") : void publish()}>
            نشر المقال
          </button>
        )}
        {hints.includes("schedule") && (
          <button className="cms-btn" type="button" disabled={busy} onClick={() => setScheduleOpen((open) => !open)}>
            جدولة النشر
          </button>
        )}
        {hints.includes("cancelSchedule") && (
          <button className="cms-btn danger" type="button" disabled={busy} onClick={cancelSchedule}>
            إلغاء الجدولة
          </button>
        )}
        {hints.includes("unpublish") && (
          <button className="cms-btn danger" type="button" disabled={busy} onClick={() =>
            roles?.includes("SystemAdmin") ? setBreakGlassAction("unpublish") : void unpublish()}>
            إلغاء النشر
          </button>
        )}
        {hints.includes("retract") && <button className="cms-btn danger" type="button" disabled={busy} onClick={() => setPostPublicationAction("retract")}>سحب الخبر</button>}
        {hints.includes("archive") && <button className="cms-btn" type="button" disabled={busy} onClick={() => setPostPublicationAction("archive")}>أرشفة</button>}
        {hints.includes("restoreArchive") && <button className="cms-btn success" type="button" disabled={busy} onClick={() => setPostPublicationAction("restoreArchive")}>إعادة إلى المنشور</button>}
        {hints.includes("reinstate") && <button className="cms-btn success" type="button" disabled={busy} onClick={() => setPostPublicationAction("reinstate")}>إعادة كمسودة</button>}
      </div>

      {article?.status === "Scheduled" && article.scheduledPublishAtUtc && (
        <p className="cms-msg" role="status">
          موعد النشر: {new Intl.DateTimeFormat("ar-EG", { dateStyle: "full", timeStyle: "short" }).format(new Date(article.scheduledPublishAtUtc))}
        </p>
      )}

      {scheduleOpen && hints.includes("schedule") && (
        <section className="cms-revision" aria-labelledby="schedule-publication-title">
          <h2 id="schedule-publication-title">جدولة النشر</h2>
          <p>لا يمكن جدولة المقال قبل اعتماده. يُعرض الموعد بتوقيت جهازك ويُحفظ بالتوقيت العالمي.</p>
          <div className="cms-field">
            <label htmlFor="scheduled-publish-at">موعد النشر</label>
            <input id="scheduled-publish-at" type="datetime-local" required min={localDateTimeInput(new Date(Date.now() + 60_000))} value={scheduleAt} onChange={(e) => setScheduleAt(e.target.value)} />
          </div>
          <div className="cms-field">
            <label htmlFor="scheduled-publish-reason">ملاحظة تحريرية (اختياري)</label>
            <textarea id="scheduled-publish-reason" rows={2} maxLength={1000} value={scheduleReason} onChange={(e) => setScheduleReason(e.target.value)} />
          </div>
          <div className="cms-actions">
            <button className="cms-btn success" type="button" disabled={busy || !scheduleAt || new Date(scheduleAt).getTime() <= Date.now()} onClick={schedulePublication}>تأكيد الجدولة</button>
            <button className="cms-btn" type="button" disabled={busy} onClick={() => setScheduleOpen(false)}>إلغاء</button>
          </div>
        </section>
      )}

      {postPublicationAction && (
        <section className="cms-revision" aria-labelledby="publication-action-title">
          <h2 id="publication-action-title">
            {postPublicationAction === "retract" ? "سحب الخبر وإظهار إشعار للقراء" :
              postPublicationAction === "archive" ? "أرشفة الخبر" :
                postPublicationAction === "restoreArchive" ? "إعادة الخبر المؤرشف إلى النشر" : "إعادة الخبر المسحوب إلى المسودات"}
          </h2>
          {postPublicationAction === "retract" && (
            <div className="cms-field">
              <label htmlFor="public-retraction-notice">الإشعار العام (10–1000 حرف، يظهر للقراء)</label>
              <textarea id="public-retraction-notice" rows={4} maxLength={1000} value={publicNotice} onChange={(e) => setPublicNotice(e.target.value)} />
            </div>
          )}
          <div className="cms-field">
            <label htmlFor="publication-action-reason">{postPublicationAction === "retract" || postPublicationAction === "reinstate" ? "السبب التحريري (إلزامي)" : "السبب (اختياري)"}</label>
            <textarea id="publication-action-reason" rows={3} maxLength={1000} value={transitionReason} onChange={(e) => setTransitionReason(e.target.value)} />
          </div>
          <div className="cms-actions">
            <button className="cms-btn danger" type="button" disabled={busy ||
              ((postPublicationAction === "retract" || postPublicationAction === "reinstate") && transitionReason.trim().length === 0) ||
              (postPublicationAction === "retract" && publicNotice.trim().length < 10)} onClick={runPostPublicationAction}>
              تأكيد وتسجيل في السجل
            </button>
            <button className="cms-btn" type="button" disabled={busy} onClick={() => setPostPublicationAction(null)}>إلغاء</button>
          </div>
        </section>
      )}

      {breakGlassAction && (
        <section className="cms-revision" aria-labelledby="break-glass-title">
          <h2 id="break-glass-title">إجراء طارئ لمدير النظام</h2>
          <p>يُسجَّل هذا الإجراء مع سبب لا يقل عن 10 أحرف في سجل المقال.</p>
          <div className="cms-field">
            <label htmlFor="break-glass-reason">السبب (10–1000 أحرف)</label>
            <textarea id="break-glass-reason" rows={3} maxLength={1000} value={breakGlassReason} onChange={(e) => setBreakGlassReason(e.target.value)} />
          </div>
          <div className="cms-actions">
            <button className="cms-btn danger" type="button" disabled={busy || breakGlassReason.trim().length < 10} onClick={async () => {
              const action = breakGlassAction;
              await (action === "publish" ? publish(breakGlassReason.trim()) : unpublish(breakGlassReason.trim()));
              setBreakGlassAction(null);
              setBreakGlassReason("");
            }}>تأكيد الإجراء الطارئ</button>
            <button className="cms-btn" type="button" disabled={busy} onClick={() => setBreakGlassAction(null)}>إلغاء</button>
          </div>
        </section>
      )}

      {article && (
        <ArticleHistory
          articleId={article.id}
          status={article.status}
          roles={roles}
          version={`${article.status}|${article.lastModifiedAtUtc ?? ""}`}
        />
      )}
    </div>
  );
}
