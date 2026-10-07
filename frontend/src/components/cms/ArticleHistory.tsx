"use client";

import { useCallback, useEffect, useState } from "react";
import {
  CORRECTION_KIND_LABEL,
  STATUS_META,
  canIssueCorrection,
  cms,
  CmsError,
  roleLabels,
  type ArticleHistory as History,
  type ArticleStatus,
  type NewCorrectionKind,
  type RevisionSnapshot,
} from "@/lib/cms-client";

const NOTE_MAX = 2000;

const when = (iso: string) =>
  new Intl.DateTimeFormat("ar-EG", { dateStyle: "medium", timeStyle: "short" }).format(new Date(iso));

const statusLabel = (value: string) => STATUS_META[value as ArticleStatus]?.label ?? value;

/**
 * Slice 20 (decision D4): the article's audit trail — workflow transitions
 * (including the reason an article was returned to its author), the versions
 * replaced by edits after publication, and the public corrections — plus the
 * form for Senior/Managing/Editor-in-Chief to issue a new correction.
 * Everything here is read from the API; role checks are hints only.
 */
export function ArticleHistory({
  articleId,
  status,
  roles,
  version,
}: {
  articleId: string;
  status: ArticleStatus;
  roles: string[] | undefined;
  /** Changes whenever the article changes, so the history reloads after every action. */
  version: string;
}) {
  const [history, setHistory] = useState<History | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [snapshot, setSnapshot] = useState<RevisionSnapshot | null>(null);
  const [kind, setKind] = useState<NewCorrectionKind>("Correction");
  const [note, setNote] = useState("");
  const [isMajor, setIsMajor] = useState(false);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      setHistory(await cms.history(articleId));
      setError(null);
    } catch (e) {
      setError(e instanceof CmsError ? e.message : "تعذّر تحميل سجل المقال.");
    }
  }, [articleId]);

  useEffect(() => {
    void load();
  }, [load, version]);

  async function openRevision(number: number) {
    if (snapshot?.revisionNumber === number) {
      setSnapshot(null);
      return;
    }
    try {
      setSnapshot(await cms.revision(articleId, number));
    } catch (e) {
      setError(e instanceof CmsError ? e.message : "تعذّر تحميل النسخة.");
    }
  }

  async function issue() {
    if (!note.trim()) return;
    setBusy(true);
    setError(null);
    try {
      await cms.issueCorrection(articleId, { kind, note: note.trim(), isMajor });
      setNote("");
      setIsMajor(false);
      await load();
    } catch (e) {
      setError(e instanceof CmsError ? e.message : "تعذّر إصدار التنويه.");
    } finally {
      setBusy(false);
    }
  }

  const showForm = (status === "Published" || status === "Archived") && canIssueCorrection(roles);

  return (
    <section className="cms-history" aria-label="سجل المقال">
      <h2>سجل المقال</h2>

      {error && (
        <div className="cms-msg error" role="alert">
          {error}
        </div>
      )}

      {history && history.transitions.length > 0 && (
        <div>
          <h3>مراحل العمل</h3>
          <ul className="cms-timeline">
            {history.transitions.map((t, i) => (
              <li key={`${t.occurredAtUtc}-${i}`}>
                <strong>
                  {statusLabel(t.fromStatus)} ← {statusLabel(t.toStatus)}
                </strong>
                <span className="cms-hint">
                  {" "}
                  • {roleLabels(t.actorRoles)} • {when(t.occurredAtUtc)}
                </span>
                {t.reason && <p className="cms-reason">السبب: {t.reason}</p>}
              </li>
            ))}
          </ul>
        </div>
      )}

      {history && history.revisions.length > 0 && (
        <div>
          <h3>نسخ سابقة (قبل تعديلات ما بعد النشر)</h3>
          <ul className="cms-timeline">
            {history.revisions.map((r) => (
              <li key={r.revisionNumber}>
                <strong>نسخة {r.revisionNumber}</strong>
                <span className="cms-hint">
                  {" "}
                  • {roleLabels(r.editorRoles)} • {when(r.editedAtUtc)}
                </span>
                {r.reason && <p className="cms-reason">السبب: {r.reason}</p>}
                <button className="cms-btn" type="button" onClick={() => void openRevision(r.revisionNumber)}>
                  {snapshot?.revisionNumber === r.revisionNumber ? "إخفاء" : "عرض النص قبل التعديل"}
                </button>
                {snapshot?.revisionNumber === r.revisionNumber && (
                  <div className="cms-snapshot">
                    <strong>{snapshot.title}</strong>
                    {snapshot.summary && <p>{snapshot.summary}</p>}
                    {/* The API returns the snapshot as HTML that was sanitized when it was stored
                        (a pre-rich-text snapshot arrives HTML-encoded), so it is safe to render. */}
                    <div className="article-body cms-snapshot-body" dangerouslySetInnerHTML={{ __html: snapshot.body }} />
                  </div>
                )}
              </li>
            ))}
          </ul>
        </div>
      )}

      {history && history.corrections.length > 0 && (
        <div>
          <h3>التنويهات والتصحيحات المنشورة</h3>
          <ul className="cms-timeline">
            {history.corrections.map((c) => (
              <li key={c.id}>
                <strong>{CORRECTION_KIND_LABEL[c.kind] ?? c.kind}</strong>
                {c.isMajor && <span className="cms-badge draft"> بارز في أعلى المقال</span>}
                <span className="cms-hint">
                  {" "}
                  • {roleLabels(c.issuerRoles)} • {when(c.issuedAtUtc)}
                </span>
                <p className="cms-reason">{c.note}</p>
              </li>
            ))}
          </ul>
        </div>
      )}

      {history &&
        history.transitions.length === 0 &&
        history.revisions.length === 0 &&
        history.corrections.length === 0 && <p className="cms-hint">لا يوجد سجل بعد.</p>}

      {showForm && (
        <div className="cms-revision">
          <h3>إضافة تنويه صحفي / تصحيح</h3>
          <p className="cms-hint">يظهر للقرّاء أسفل المقال (أو أعلاه إذا كان بارزاً) ولا يمكن تعديله أو حذفه لاحقاً.</p>
          <div className="cms-field">
            <label htmlFor="corr-kind">النوع</label>
            <select id="corr-kind" value={kind} onChange={(e) => setKind(e.target.value as NewCorrectionKind)}>
              <option value="Correction">تصحيح</option>
              <option value="Clarification">توضيح</option>
              <option value="EditorNote">تنويه المحرر</option>
              <option value="Update">تحديث</option>
            </select>
          </div>
          <div className="cms-field">
            <label htmlFor="corr-note">نص التنويه</label>
            <textarea id="corr-note" rows={3} value={note} maxLength={NOTE_MAX} onChange={(e) => setNote(e.target.value)} />
            <span className="cms-hint">
              {note.length} / {NOTE_MAX}
            </span>
          </div>
          <label className="cms-check">
            <input type="checkbox" checked={isMajor} onChange={(e) => setIsMajor(e.target.checked)} /> خطأ جسيم — اعرضه في أعلى المقال
          </label>
          <div className="cms-actions">
            <button className="cms-btn primary" type="button" disabled={busy || !note.trim()} onClick={() => void issue()}>
              نشر التنويه
            </button>
          </div>
        </div>
      )}
    </section>
  );
}
