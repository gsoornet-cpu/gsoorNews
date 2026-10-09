"use client";

import { useEffect, useRef, useState, type ReactNode } from "react";
import Image from "next/image";
import { EditorContent, Node, useEditor, useEditorState } from "@tiptap/react";
import StarterKit from "@tiptap/starter-kit";
import { Table, TableCell, TableHeader, TableRow } from "@tiptap/extension-table";
import { countWords, htmlToPlainText } from "@/lib/html";
import { estimateReadMinutes } from "@/lib/format";
import { cmsMedia, type CmsMediaAsset } from "@/lib/cms-media-client";

/** Mirrors EditorialArticle.BodyMaxLength on the API (characters of stored HTML). */
export const BODY_HTML_MAX = 100_000;

/**
 * Slice 21, decision D2 — the CMS body editor (Tiptap / ProseMirror, RTL).
 *
 * The editor only offers what the server's allow-list accepts (p, br, h2, h3, strong, em, u,
 * blockquote, ul, ol, li, a, hr and simple table markup). That is a convenience, NOT a security control: the API
 * sanitizes every save and stores only the sanitized result, so a hand-crafted request gets no
 * further than a pasted payload would. If the allow-list ever grows (Slice 22: images, embeds),
 * this extension list grows with it.
 *
 * `value` is HTML. The editor reports "" (never "<p></p>") when it holds no content, so the
 * parent's "body is required" rule works unchanged.
 */
export function RichTextEditor({
  id,
  value,
  onChange,
  disabled = false,
}: {
  id: string;
  value: string;
  onChange: (html: string) => void;
  disabled?: boolean;
}) {
  const [linkError, setLinkError] = useState<string | null>(null);
  const [mediaOpen, setMediaOpen] = useState(false);
  const [mediaBusy, setMediaBusy] = useState(false);
  const [mediaError, setMediaError] = useState<string | null>(null);
  const [mediaItems, setMediaItems] = useState<CmsMediaAsset[]>([]);
  const [mediaLibraryExpanded, setMediaLibraryExpanded] = useState(false);
  const [mediaPage, setMediaPage] = useState(1);
  const [mediaTotalCount, setMediaTotalCount] = useState(0);
  const [mediaProgress, setMediaProgress] = useState<number | null>(null);
  const [altText, setAltText] = useState("");
  const [credit, setCredit] = useState("");
  const [caption, setCaption] = useState("");
  const [focalX, setFocalX] = useState("");
  const [focalY, setFocalY] = useState("");
  const [selectedImage, setSelectedImage] = useState(false);
  // The last HTML this editor itself reported. A `value` that differs from it came from outside
  // (e.g. the server returned the sanitized body after a save) and must replace the content;
  // a `value` that equals it is just our own keystroke coming back and must not move the cursor.
  const lastEmitted = useRef(value);

  const editor = useEditor({
    // Next.js renders client components on the server too; Tiptap must not touch the DOM there.
    immediatelyRender: false,
    extensions: [
      StarterKit.configure({
        heading: { levels: [2, 3] },
        // Everything the server allow-list would strip anyway:
        code: false,
        codeBlock: false,
        strike: false,
        link: {
          openOnClick: false,
          autolink: true,
          defaultProtocol: "https",
          protocols: ["mailto"],
          HTMLAttributes: { rel: "noopener noreferrer", target: null },
        },
      }),
      MediaFigure,
      MediaVideo,
      MediaEmbed,
      Table.configure({ resizable: false, renderWrapper: true, lastColumnResizable: false }),
      TableRow,
      TableHeader.extend({ content: "paragraph+" }).configure({ HTMLAttributes: {} }),
      TableCell.extend({ content: "paragraph+" }).configure({ HTMLAttributes: {} }),
    ],
    content: value,
    editable: !disabled,
    editorProps: {
      attributes: {
        id,
        dir: "rtl",
        lang: "ar",
        role: "textbox",
        "aria-multiline": "true",
        "aria-labelledby": `${id}-label`,
        class: "rte-content article-body",
      },
    },
    onUpdate: ({ editor: e }) => {
      const html = e.isEmpty ? "" : e.getHTML();
      lastEmitted.current = html;
      onChange(html);
    },
  });

  useEffect(() => {
    editor?.setEditable(!disabled);
  }, [editor, disabled]);

  useEffect(() => {
    if (editor && value !== lastEmitted.current) {
      lastEmitted.current = value;
      editor.commands.setContent(value, { emitUpdate: false });
    }
  }, [editor, value]);

  // Active-state of toolbar buttons and the counters, recomputed on every transaction.
  const state = useEditorState({
    editor,
    selector: ({ editor: e }) => {
      if (!e) {
        return null;
      }

      const text = htmlToPlainText(e.isEmpty ? "" : e.getHTML());
      return {
        bold: e.isActive("bold"),
        italic: e.isActive("italic"),
        underline: e.isActive("underline"),
        h2: e.isActive("heading", { level: 2 }),
        h3: e.isActive("heading", { level: 3 }),
        quote: e.isActive("blockquote"),
        bullets: e.isActive("bulletList"),
        numbers: e.isActive("orderedList"),
        link: e.isActive("link"),
        table: e.isActive("table"),
        canUndo: e.can().undo(),
        canRedo: e.can().redo(),
        words: countWords(text),
        characters: text.length,
        readMinutes: estimateReadMinutes(text),
        htmlLength: e.isEmpty ? 0 : e.getHTML().length,
      };
    },
  });

  if (!editor || !state) {
    return <div className="rte rte-loading" aria-busy="true">جارٍ تحميل المحرر…</div>;
  }

  function setLink() {
    if (!editor) {
      return;
    }

    const previous = (editor.getAttributes("link").href as string | undefined) ?? "";
    const entered = window.prompt("أدخل الرابط (http أو https أو mailto):", previous);
    if (entered === null) {
      return; // cancelled
    }

    const href = normalizeLinkHref(entered);
    if (href === null) {
      editor.chain().focus().extendMarkRange("link").unsetLink().run();
      setLinkError(null);
      return;
    }

    if (href === false) {
      setLinkError("رابط غير مقبول. استخدم عنوانًا يبدأ بـ http أو https أو mailto، أو مسارًا داخليًا يبدأ بـ /.");
      return;
    }

    setLinkError(null);
    editor.chain().focus().extendMarkRange("link").setLink({ href }).run();
  }

  function setEmbed() {
    if (!editor) return;
    const value = window.prompt("ألصق رابط فيديو YouTube أو رابط التضمين الرسمي من Vimeo أو X أو Instagram أو Facebook أو TikTok:");
    if (!value) return;
    const safe = normalizeEmbedUrl(value.trim());
    if (!safe) { setMediaError("رابط التضمين غير مدعوم. استخدم رابط embed رسميًا من أحد المزودين المعتمدين."); setMediaOpen(true); return; }
    setMediaError(null);
    editor.chain().focus().insertContent({ type: "mediaEmbed", attrs: safe }).run();
  }

  async function openMediaLibrary() {
    setMediaError(null);
    setMediaOpen(true);
    setMediaLibraryExpanded(false);
    setMediaPage(1);
    try {
      const page = await cmsMedia.list(1, 24);
      setMediaItems(page.items);
      setMediaTotalCount(page.totalCount);
    }
    catch (error) { setMediaError(error instanceof Error ? error.message : "تعذّر تحميل مكتبة الوسائط."); }
  }

  function expandMediaLibrary() {
    setMediaLibraryExpanded(true);
  }

  async function loadMoreMedia() {
    const nextPage = mediaPage + 1;
    setMediaBusy(true);
    try {
      const page = await cmsMedia.list(nextPage, 24);
      setMediaItems((current) => [...current, ...page.items]);
      setMediaPage(nextPage);
    } catch (error) { setMediaError(error instanceof Error ? error.message : "تعذّر تحميل المزيد من الوسائط."); }
    finally { setMediaBusy(false); }
  }

  function insertMedia(asset: CmsMediaAsset) {
    if (!editor) return;
    if (asset.kind === "Image") {
      editor.chain().focus().insertContent({ type: "mediaFigure", attrs: {
        mediaId: asset.id, src: asset.publicUrl, alt: asset.altText, caption: asset.caption ?? "", credit: asset.credit,
      } }).run();
    } else if (asset.kind === "Video") {
      editor.chain().focus().insertContent({ type: "mediaVideo", attrs: {
        mediaId: asset.id, src: asset.publicUrl, caption: asset.caption ?? "", credit: asset.credit,
      } }).run();
    } else {
      editor.chain().focus().insertContent(`<p><a href="${escapeAttribute(asset.publicUrl)}" data-media-id="${asset.id}" rel="noopener noreferrer">${escapeText(asset.caption || asset.fileName)} · ${escapeText(asset.credit)}</a></p>`).run();
    }
    setMediaOpen(false);
  }

  async function uploadMedia(formData: FormData) {
    const file = formData.get("media-file");
    if (!(file instanceof File)) return;
    const isImage = file.type.startsWith("image/");
    const nextAlt = altText.trim();
    const nextCredit = credit.trim();
    if (!nextCredit || (isImage && !nextAlt)) {
      setMediaError(isImage ? "أدخل وصفًا بديلًا للصورة واسم المصدر/المصور." : "أدخل اسم المصدر/المنتج.");
      return;
    }
    setMediaBusy(true); setMediaError(null); setMediaProgress(0);
    try {
      const asset = await cmsMedia.upload(file, { altText: nextAlt || caption.trim() || file.name, credit: nextCredit,
        caption: caption.trim(), focalPointX: focalX === "" ? null : Number(focalX), focalPointY: focalY === "" ? null : Number(focalY) }, setMediaProgress);
      insertMedia(asset);
    } catch (error) { setMediaError(error instanceof Error ? error.message : "فشل رفع الوسيط."); }
    finally { setMediaBusy(false); setMediaProgress(null); }
  }

  const tooLong = state.htmlLength > BODY_HTML_MAX;
  const nearLimit = state.htmlLength > BODY_HTML_MAX * 0.9;

  return (
    <div className="rte">
      <div className="rte-toolbar" role="toolbar" aria-label="تنسيق النص" aria-controls={id}>
        <ToolButton label="إدراج جدول بعناوين وعمودين" active={false} disabled={disabled || state.table} onClick={() => editor.chain().focus().insertTable({ rows: 3, cols: 2, withHeaderRow: true }).run()}>
          ▦
        </ToolButton>
        <span className="rte-sep" aria-hidden="true" />
        <ToolButton label="غامق" active={state.bold} disabled={disabled} onClick={() => editor.chain().focus().toggleBold().run()}>
          <strong>B</strong>
        </ToolButton>
        <ToolButton label="مائل" active={state.italic} disabled={disabled} onClick={() => editor.chain().focus().toggleItalic().run()}>
          <em>I</em>
        </ToolButton>
        <ToolButton label="تسطير" active={state.underline} disabled={disabled} onClick={() => editor.chain().focus().toggleUnderline().run()}>
          <u>U</u>
        </ToolButton>
        <span className="rte-sep" aria-hidden="true" />
        <ToolButton label="عنوان رئيسي" active={state.h2} disabled={disabled} onClick={() => editor.chain().focus().toggleHeading({ level: 2 }).run()}>
          H2
        </ToolButton>
        <ToolButton label="عنوان فرعي" active={state.h3} disabled={disabled} onClick={() => editor.chain().focus().toggleHeading({ level: 3 }).run()}>
          H3
        </ToolButton>
        <ToolButton label="اقتباس" active={state.quote} disabled={disabled} onClick={() => editor.chain().focus().toggleBlockquote().run()}>
          ❝
        </ToolButton>
        <span className="rte-sep" aria-hidden="true" />
        <ToolButton label="قائمة نقطية" active={state.bullets} disabled={disabled} onClick={() => editor.chain().focus().toggleBulletList().run()}>
          •≡
        </ToolButton>
        <ToolButton label="قائمة مرقّمة" active={state.numbers} disabled={disabled} onClick={() => editor.chain().focus().toggleOrderedList().run()}>
          1.
        </ToolButton>
        <ToolButton label="خط فاصل" active={false} disabled={disabled} onClick={() => editor.chain().focus().setHorizontalRule().run()}>
          ―
        </ToolButton>
        <span className="rte-sep" aria-hidden="true" />
        <ToolButton label={state.link ? "تعديل الرابط أو إزالته" : "إضافة رابط"} active={state.link} disabled={disabled} onClick={setLink}>
          🔗
        </ToolButton>
        <ToolButton label="مكتبة الوسائط وإدراج ملف" active={mediaOpen} disabled={disabled} onClick={() => void openMediaLibrary()}>
          ▧
        </ToolButton>
        <ToolButton label="إدراج محتوى مضمن معتمد" active={false} disabled={disabled} onClick={setEmbed}>▣</ToolButton>
        <span className="rte-sep" aria-hidden="true" />
        <ToolButton label="تراجع" active={false} disabled={disabled || !state.canUndo} onClick={() => editor.chain().focus().undo().run()}>
          ↶
        </ToolButton>
        <ToolButton label="إعادة" active={false} disabled={disabled || !state.canRedo} onClick={() => editor.chain().focus().redo().run()}>
          ↷
        </ToolButton>
      </div>

      {linkError && (
        <div className="cms-msg error" role="alert">
          {linkError}
        </div>
      )}

      {mediaOpen && <section className="cms-media-picker" aria-label="مكتبة الوسائط" aria-live="polite">
        <div className="cms-media-picker-head"><strong>إضافة صورة أو ملف إلى موضع الكتابة</strong><button type="button" onClick={() => setMediaOpen(false)}>إغلاق</button></div>
        <form onSubmit={event => { event.preventDefault(); void uploadMedia(new FormData(event.currentTarget)); }}>
          <label>اختر صورة أو فيديو أو PDF<input name="media-file" type="file" accept="image/jpeg,image/png,image/webp,image/avif,video/mp4,video/webm,application/pdf" required disabled={mediaBusy} onChange={event => { const isImage = event.target.files?.[0]?.type.startsWith("image/") ?? false; setSelectedImage(isImage); if (!isImage) { setFocalX(""); setFocalY(""); } }} /></label>
          <label>الوصف البديل أو وصف الملف<input value={altText} onChange={e => setAltText(e.target.value)} maxLength={500} required /></label>
          <label>المصدر / المصور<input value={credit} onChange={e => setCredit(e.target.value)} maxLength={300} required /></label>
          <label>التعليق (اختياري)<input value={caption} onChange={e => setCaption(e.target.value)} maxLength={500} /></label>
          <div className="cms-media-focal"><label>موضع الصورة أفقيًا %<input type="number" min={0} max={100} value={focalX} disabled={!selectedImage} onChange={e => setFocalX(e.target.value)} /></label><label>رأسيًا %<input type="number" min={0} max={100} value={focalY} disabled={!selectedImage} onChange={e => setFocalY(e.target.value)} /></label></div>
          <button type="submit" disabled={mediaBusy}>{mediaBusy ? `جارٍ الرفع ${mediaProgress ?? 0}%` : "رفع وإدراج"}</button>
        </form>
        {mediaError && <p role="alert" className="cms-msg error">{mediaError}</p>}
        <div className="cms-media-grid">{(mediaLibraryExpanded ? mediaItems : mediaItems.slice(0, 6)).map(asset => <button className="cms-media-card" key={asset.id} type="button" disabled={mediaBusy} onClick={() => insertMedia(asset)}>
          {asset.kind === "Image" ? <Image src={asset.publicUrl} alt="" width={260} height={176} unoptimized /> : <span className="cms-media-file-icon">{asset.kind === "Video" ? "▶" : "PDF"}</span>}
          <span>{asset.caption || asset.fileName}</span>
        </button>)}</div>
        {!mediaLibraryExpanded && mediaTotalCount > 6 && <button type="button" className="cms-btn" onClick={expandMediaLibrary}>فتح مكتبة الوسائط كاملة ({mediaTotalCount.toLocaleString("ar-EG")})</button>}
        {mediaLibraryExpanded && mediaItems.length < mediaTotalCount && <button type="button" className="cms-btn" disabled={mediaBusy} onClick={() => void loadMoreMedia()}>{mediaBusy ? "جارٍ تحميل الملفات…" : "تحميل المزيد"}</button>}
      </section>}

      <EditorContent editor={editor} />

      <div className="rte-counters cms-hint" aria-live="off">
        <span>{state.words} كلمة</span>
        <span>{state.characters} حرف</span>
        <span>
          {state.readMinutes} {state.readMinutes === 1 ? "دقيقة قراءة" : "دقائق قراءة"}
        </span>
        {nearLimit && (
          <span className={tooLong ? "rte-over" : "rte-warn"} role={tooLong ? "alert" : undefined}>
            {tooLong
              ? `النص أطول من الحد المسموح (${BODY_HTML_MAX.toLocaleString("ar-EG")} حرف من الترميز). اختصر النص قبل الحفظ.`
              : "اقترب النص من الحد الأقصى المسموح."}
          </span>
        )}
      </div>
    </div>
  );
}

const MediaFigure = Node.create({
  name: "mediaFigure", group: "block", atom: true, selectable: true,
  addAttributes() { return {
    mediaId: { default: null, parseHTML: (el: HTMLElement) => el.getAttribute("data-media-id") },
    src: { default: null, parseHTML: (el: HTMLElement) => el.querySelector("img")?.getAttribute("src") },
    alt: { default: "", parseHTML: (el: HTMLElement) => el.querySelector("img")?.getAttribute("alt") ?? "" },
    caption: { default: "", parseHTML: (el: HTMLElement) => el.querySelector("figcaption")?.textContent ?? "" },
    credit: { default: "", parseHTML: (el: HTMLElement) => el.querySelector("figcaption")?.textContent ?? "" },
  }; },
  parseHTML() { return [{ tag: "figure[data-media-id]", getAttrs: el => el.querySelector("img") ? null : false }]; },
  renderHTML({ HTMLAttributes }) {
    const { mediaId, src, alt, caption } = HTMLAttributes;
    const description = alt || caption || "";
    return ["figure", { "data-media-id": mediaId }, ["img", { src, alt, "data-media-id": mediaId }], ["figcaption", description]];
  },
});

const MediaEmbed = Node.create({
  name: "mediaEmbed", group: "block", atom: true, selectable: true,
  addAttributes() { return {
    src: { default: null, parseHTML: (el: HTMLElement) => el.getAttribute("src") },
    provider: { default: "", parseHTML: (el: HTMLElement) => el.getAttribute("data-provider") },
  }; },
  parseHTML() { return [{ tag: "iframe[data-provider]" }]; },
  renderHTML({ HTMLAttributes }) { return ["iframe", {
    src: HTMLAttributes.src, "data-provider": HTMLAttributes.provider,
    title: `محتوى مضمن من ${HTMLAttributes.provider}`, loading: "lazy",
    referrerpolicy: "strict-origin-when-cross-origin", sandbox: "allow-scripts allow-same-origin allow-presentation",
    allow: "encrypted-media; picture-in-picture", allowfullscreen: "",
  }]; },
});

const MediaVideo = Node.create({
  name: "mediaVideo", group: "block", atom: true, selectable: true,
  addAttributes() { return {
    mediaId: { default: null, parseHTML: (el: HTMLElement) => el.getAttribute("data-media-id") },
    src: { default: null, parseHTML: (el: HTMLElement) => el.querySelector("video")?.getAttribute("src") },
    caption: { default: "", parseHTML: (el: HTMLElement) => el.querySelector("figcaption")?.textContent ?? "" },
    credit: { default: "", parseHTML: (el: HTMLElement) => el.querySelector("figcaption")?.textContent ?? "" },
  }; },
  parseHTML() { return [{ tag: "figure[data-media-id]", getAttrs: el => el.querySelector("video") ? null : false }]; },
  renderHTML({ HTMLAttributes }) {
    const { mediaId, src, caption, credit } = HTMLAttributes;
    return ["figure", { "data-media-id": mediaId }, ["video", { src, "data-media-id": mediaId, controls: "", preload: "metadata", playsinline: "" }], ["figcaption", [caption, credit].filter(Boolean).join(" · ")]];
  },
});

function normalizeEmbedUrl(value: string): { src: string; provider: string } | null {
  try {
    const u = new URL(value);
    if (u.protocol !== "https:" || u.username || u.password || u.port || u.hash) return null;
    const path = u.pathname.replace(/\/$/, "");
    let provider = "";
    if (["youtube.com", "www.youtube.com", "m.youtube.com", "youtube-nocookie.com", "www.youtube-nocookie.com"].includes(u.hostname)) {
      const embed = path.match(/^\/(?:embed|shorts|live)\/([\w-]{11})$/);
      const watch = path === "/watch" ? u.searchParams.get("v") : null;
      const id = embed?.[1] ?? watch;
      if (id && /^[\w-]{11}$/.test(id)) {
        u.hostname = "www.youtube-nocookie.com";
        u.pathname = `/embed/${id}`;
        u.search = "";
        provider = "YouTube";
      }
    } else if (u.hostname === "youtu.be" && /^\/[\w-]{11}$/.test(path)) {
      u.hostname = "www.youtube-nocookie.com";
      u.pathname = `/embed/${path.slice(1)}`;
      u.search = "";
      provider = "YouTube";
    }
    else if (u.hostname === "player.vimeo.com" && /^\/video\/\d{1,20}$/.test(path)) provider = "Vimeo";
    else if (u.hostname === "platform.twitter.com" && path === "/embed/Tweet.html" && /^\d{1,30}$/.test(u.searchParams.get("id") ?? "")) provider = "X";
    else if (u.hostname === "www.instagram.com" && /^\/(p|reel)\/[\w-]{3,40}\/embed$/.test(path)) provider = "Instagram";
    else if (u.hostname === "www.facebook.com" && path === "/plugins/video.php" && isFacebookUrl(u.searchParams.get("href") ?? "")) provider = "Facebook";
    else if (u.hostname === "www.tiktok.com" && /^\/embed\/v2\/\d{5,30}$/.test(path)) provider = "TikTok";
    if (!provider) return null;
    const normalized = new URL(value); normalized.pathname = path;
    for (const key of [...normalized.searchParams.keys()]) if (provider === "X" ? key !== "id" : provider === "Facebook" ? key !== "href" : true) normalized.searchParams.delete(key);
    return { src: normalized.toString(), provider };
  } catch { return null; }
}

function isFacebookUrl(value: string) {
  try { const u = new URL(value); return u.protocol === "https:" && u.hostname === "www.facebook.com" && !u.username && !u.password && !u.port && (/^\/watch$/.test(u.pathname) || /^\/(videos|posts|reel)\//.test(u.pathname) || /^\/permalink\.php$/.test(u.pathname)); }
  catch { return false; }
}

function escapeAttribute(value: string) { return value.replaceAll("&", "&amp;").replaceAll("\"", "&quot;").replaceAll("<", "&lt;").replaceAll(">", "&gt;"); }
function escapeText(value: string) { return value.replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;"); }

function ToolButton({
  label,
  active,
  disabled,
  onClick,
  children,
}: {
  label: string;
  active: boolean;
  disabled: boolean;
  onClick: () => void;
  children: ReactNode;
}) {
  return (
    <button
      type="button"
      className={`rte-btn${active ? " active" : ""}`}
      aria-label={label}
      title={label}
      aria-pressed={active}
      disabled={disabled}
      // Keep the text selection: a mousedown on a button would otherwise blur the editor first.
      onMouseDown={(event) => event.preventDefault()}
      onClick={onClick}
    >
      {children}
    </button>
  );
}

/**
 * Normalises an editor-entered link target.
 *  - string: a safe href to apply
 *  - null:   empty input — remove the link
 *  - false:  rejected (unsupported scheme)
 * Allowed: http(s)://, mailto:, a site-relative path ("/…"), or a bare domain (gets https://).
 * The server re-validates; this only gives the editor an immediate, readable error.
 */
export function normalizeLinkHref(input: string): string | null | false {
  const value = input.trim();
  if (!value) {
    return null;
  }

  if (/^https?:\/\//i.test(value) || /^mailto:[^\s@]+@[^\s@]+$/i.test(value)) {
    return value;
  }

  if (value.startsWith("/") && !value.startsWith("//")) {
    return value;
  }

  // Any other explicit scheme (javascript:, data:, vbscript:, ftp: …) is refused. A bare
  // "example.com:8080/path" is not a scheme, so only reject when what precedes ":" is not a host:port.
  if (/^[a-z][a-z0-9+.-]*:/i.test(value) && !/^[^\s/:]+\.[^\s/:]+:\d+(\/|$)/.test(value)) {
    return false;
  }

  if (/^[^\s/@]+\.[^\s/@]+(\/\S*)?$/.test(value)) {
    return `https://${value}`;
  }

  return false;
}
