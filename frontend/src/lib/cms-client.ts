// Browser-side client for the CMS. Talks ONLY to this app's own /cms-api
// proxy (same origin); the JWT lives in an httpOnly cookie the browser
// attaches automatically and JavaScript can never read.

export type ArticleStatus = "Draft" | "InReview" | "Approved" | "Scheduled" | "Published" | "Retracted" | "Archived";

/** Arabic label + badge style for each workflow state (Slice 19, decision D1). */
export const STATUS_META: Record<ArticleStatus, { label: string; badge: string }> = {
  Draft: { label: "مسودة", badge: "draft" },
  InReview: { label: "قيد المراجعة", badge: "review" },
  Approved: { label: "معتمد", badge: "approved" },
  Scheduled: { label: "مجدول للنشر", badge: "scheduled" },
  Published: { label: "منشور", badge: "published" },
  Retracted: { label: "مسحوب", badge: "retracted" },
  Archived: { label: "مؤرشف", badge: "archived" },
};

export interface CmsArticleSummary {
  id: string;
  title: string;
  authorName?: string | null;
  summary: string | null;
  status: ArticleStatus;
  ownerUserId: string;
  createdAtUtc: string;
  lastModifiedAtUtc: string | null;
  publishedAtUtc: string | null;
  scheduledPublishAtUtc?: string | null;
}

export interface CmsArticle extends CmsArticleSummary {
  body: string;
  countryId: string | null;
  cityId: string | null;
  slug?: string;
  presentationDesks: string[];
  featuredVideoMediaAssetId: string | null;
  socialImageUrl?: string | null;
}

export interface CmsCategory { id: string; nameAr: string; slug: string; displayOrder: number; }
export interface CmsSeoArticleSummary { id: string; title: string; slug: string; status: ArticleStatus; noIndex: boolean; primaryCategoryId: string | null; }
export interface CmsArticleSeo {
  id: string; title: string; slug: string; seoTitle: string | null; seoDescription: string | null;
  canonicalUrl: string | null; socialTitle: string | null; socialDescription: string | null;
  socialImageUrl: string | null; noIndex: boolean; noFollow: boolean; status: ArticleStatus;
  primaryCategoryId: string | null; secondaryTags: string[]; categories: CmsCategory[];
  twitterTitle: string | null; twitterDescription: string | null; twitterImageUrl: string | null;
}
export type CmsArticleSeoInput = Omit<CmsArticleSeo, "id" | "title" | "status" | "categories">;

// ---- Slice 20 (decision D4): history, revisions, corrections ----

/** Historical rows may still contain Notice (enum value 2); new corrections cannot issue it. */
export type CorrectionKind = "Correction" | "Notice" | "Clarification" | "EditorNote" | "Update";
export type NewCorrectionKind = Exclude<CorrectionKind, "Notice">;

export const CORRECTION_KIND_LABEL: Record<CorrectionKind, string> = {
  Correction: "تصحيح",
  Notice: "تنويه",
  Clarification: "توضيح",
  EditorNote: "تنويه المحرر",
  Update: "تحديث",
};

export interface HistoryTransition {
  fromStatus: string;
  toStatus: string;
  actorUserId: string;
  actorRoles: string;
  reason: string | null;
  occurredAtUtc: string;
}

export interface HistoryRevision {
  revisionNumber: number;
  title: string;
  editedByUserId: string;
  editorRoles: string;
  reason: string | null;
  editedAtUtc: string;
}

/** The article exactly as it read BEFORE the edit that created this revision. */
export interface RevisionSnapshot extends HistoryRevision {
  summary: string | null;
  authorName: string | null;
  body: string;
  countryId: string | null;
  cityId: string | null;
}

export interface CmsCorrection {
  id: string;
  kind: CorrectionKind;
  note: string;
  isMajor: boolean;
  issuedByUserId: string;
  issuerRoles: string;
  issuedAtUtc: string;
}

export interface ArticleHistory {
  transitions: HistoryTransition[];
  revisions: HistoryRevision[];
  corrections: CmsCorrection[];
}

export interface CmsPaged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CmsSession {
  authenticated: boolean;
  email?: string | null;
  roles?: string[];
}

export class CmsError extends Error {
  constructor(
    message: string,
    public status: number,
  ) {
    super(message);
  }
}

const FRIENDLY: Record<number, string> = {
  401: "انتهت الجلسة. سجّل الدخول من جديد.",
  403: "ليست لديك صلاحية لتنفيذ هذا الإجراء على هذا المقال.",
  404: "المقال غير موجود.",
  409: "لا يمكن تنفيذ الإجراء في الحالة الحالية للمقال.",
};

async function readError(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as { message?: string; errors?: { message: string }[] };
    if (body.errors?.length) {
      return body.errors.map((e) => e.message).join(" • ");
    }
    if (response.status === 400 && body.message) {
      return body.message;
    }
  } catch {
    // fall through to the generic message
  }

  return FRIENDLY[response.status] ?? "حدث خطأ غير متوقع. حاول مرة أخرى.";
}

async function call<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/cms-api/articles${path}`, {
    ...init,
    credentials: "same-origin",
    cache: "no-store",
    headers: { "Content-Type": "application/json", ...init?.headers },
  });

  if (!response.ok) {
    throw new CmsError(await readError(response), response.status);
  }

  return (await response.json()) as T;
}

export interface CmsHomeVideo { articleId: string; title: string; publishedAtUtc: string; thumbnailUrl: string | null; homeVideoOrder: number | null; }

export const cmsHomeVideos = {
  async list(): Promise<CmsHomeVideo[]> {
    const response = await fetch("/cms-api/home-videos", { credentials: "same-origin", cache: "no-store" });
    if (!response.ok) throw new CmsError(await readError(response), response.status);
    return (await response.json()) as CmsHomeVideo[];
  },
  async save(articleIds: string[]): Promise<void> {
    const response = await fetch("/cms-api/home-videos", {
      method: "PUT", credentials: "same-origin", cache: "no-store",
      headers: { "Content-Type": "application/json" }, body: JSON.stringify({ articleIds }),
    });
    if (!response.ok) throw new CmsError(await readError(response), response.status);
  },
};

export const cms = {
  getSeo: (id: string) => call<CmsArticleSeo>(`/${id}/seo`),
  listCategories: async () => {
    const response = await fetch("/cms-api/editorial-categories", { credentials: "same-origin", cache: "no-store" });
    if (!response.ok) throw new CmsError(await readError(response), response.status);
    return response.json() as Promise<CmsCategory[]>;
  },
  listSeoArticles: async (page = 1, pageSize = 50) => {
    const response = await fetch(`/cms-api/seo-articles?page=${page}&pageSize=${pageSize}`, { credentials: "same-origin", cache: "no-store" });
    if (!response.ok) throw new CmsError(await readError(response), response.status);
    return response.json() as Promise<{ items: CmsSeoArticleSummary[]; page: number; pageSize: number; totalCount: number }>;
  },
  createCategory: (input: { nameAr: string; slug: string; displayOrder: number }) => categoryCall("", { method: "POST", body: JSON.stringify(input) }),
  deactivateCategory: (id: string) => categoryCall(`/${id}`, { method: "DELETE" }),
  updateSeo: (id: string, input: CmsArticleSeoInput) =>
    call<CmsArticleSeo>(`/${id}/seo`, { method: "PUT", body: JSON.stringify(input) }),
  async session(): Promise<CmsSession> {
    const response = await fetch("/cms-api/session", { credentials: "same-origin", cache: "no-store" });
    return response.ok ? ((await response.json()) as CmsSession) : { authenticated: false };
  },

  async login(email: string, password: string): Promise<void> {
    const response = await fetch("/cms-api/session", {
      method: "POST",
      credentials: "same-origin",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password }),
    });

    if (!response.ok) {
      const body = (await response.json().catch(() => ({}))) as { message?: string };
      throw new CmsError(body.message ?? "تعذّر تسجيل الدخول.", response.status);
    }
  },

  async logout(): Promise<void> {
    await fetch("/cms-api/session", { method: "DELETE", credentials: "same-origin" });
  },

  list: (status?: ArticleStatus, page = 1) =>
    call<CmsPaged<CmsArticleSummary>>(`?page=${page}&pageSize=20${status ? `&status=${status}` : ""}`),

  get: (id: string) => call<CmsArticle>(`/${id}`),

  create: (input: ArticleInput) => call<CmsArticle>("", { method: "POST", body: JSON.stringify(input) }),

  update: (id: string, input: ArticleInput) =>
    call<CmsArticle>(`/${id}`, { method: "PUT", body: JSON.stringify(input) }),

  submit: (id: string) => call<CmsArticle>(`/${id}/submit`, { method: "POST" }),

  requestRevision: (id: string, reason: string) =>
    call<CmsArticle>(`/${id}/request-revision`, { method: "POST", body: JSON.stringify({ reason }) }),

  approve: (id: string) => call<CmsArticle>(`/${id}/approve`, { method: "POST" }),

  publish: (id: string, reason?: string) => call<CmsArticle>(`/${id}/publish`, {
    method: "POST",
    ...(reason === undefined ? {} : { body: JSON.stringify({ reason }) }),
  }),

  schedule: (id: string, scheduledPublishAtUtc: string, reason?: string) => call<CmsArticle>(`/${id}/schedule`, {
    method: "POST", body: JSON.stringify({ scheduledPublishAtUtc, reason: reason || null }),
  }),

  cancelSchedule: (id: string, reason?: string) => call<CmsArticle>(`/${id}/cancel-schedule`, {
    method: "POST", body: JSON.stringify({ reason: reason || null }),
  }),

  unpublish: (id: string, reason?: string) => call<CmsArticle>(`/${id}/unpublish`, {
    method: "POST",
    ...(reason === undefined ? {} : { body: JSON.stringify({ reason }) }),
  }),

  retract: (id: string, publicNotice: string, internalReason: string) =>
    call<CmsArticle>(`/${id}/retract`, { method: "POST", body: JSON.stringify({ publicNotice, internalReason }) }),

  archive: (id: string, reason: string) =>
    call<CmsArticle>(`/${id}/archive`, { method: "POST", body: JSON.stringify({ reason: reason || null }) }),

  restoreArchive: (id: string, reason: string) =>
    call<CmsArticle>(`/${id}/restore-archive`, { method: "POST", body: JSON.stringify({ reason: reason || null }) }),

  reinstate: (id: string, reason: string) =>
    call<CmsArticle>(`/${id}/reinstate`, { method: "POST", body: JSON.stringify({ reason }) }),

  history: (id: string) => call<ArticleHistory>(`/${id}/history`),

  revision: (id: string, number: number) => call<RevisionSnapshot>(`/${id}/revisions/${number}`),

  issueCorrection: (id: string, input: { kind: NewCorrectionKind; note: string; isMajor: boolean }) =>
    call<CmsCorrection>(`/${id}/corrections`, { method: "POST", body: JSON.stringify(input) }),
};

async function categoryCall<T = unknown>(path: string, init: RequestInit): Promise<T> {
  const response = await fetch(`/cms-api/editorial-categories${path}`, { ...init, credentials: "same-origin", cache: "no-store", headers: { "Content-Type": "application/json", ...init.headers } });
  if (!response.ok) throw new CmsError(await readError(response), response.status);
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export interface ArticleInput {
  title: string;
  authorName: string | null;
  summary: string | null;
  body: string;
  countryId: string | null;
  cityId: string | null;
  /** Optional note stored with the revision when a PUBLISHED article is edited (decision D4). */
  editReason?: string | null;
  presentationDesks: string[];
  featuredVideoMediaAssetId: string | null;
  /** Cover image URL. "" removes it; omitted leaves it unchanged. Saved with the article itself (no SEO role needed). */
  coverImageUrl?: string | null;
}

export const PRESENTATION_DESKS = [
  { slug: "mughtarib", label: "أخبار المغتربين" },
  { slug: "success", label: "قصة نجاح" },
  { slug: "egypt", label: "أخبار مصر" },
  { slug: "opportunities", label: "فرص استثمارية" },
  { slug: "official", label: "مع مسئول" },
  { slug: "events", label: "فعاليات" },
  { slug: "red", label: "خط أحمر" },
  { slug: "lamma", label: "اللمة الحلوة" },
  { slug: "secondgen", label: "الجيل الثاني" },
  { slug: "sports", label: "رياضة" },
  { slug: "arts", label: "فنون" },
  { slug: "articles", label: "مقالات رأي" },
  { slug: "various", label: "منوعات" },
] as const;

export type WorkflowAction = "submit" | "requestRevision" | "approve" | "publish" | "schedule" | "cancelSchedule" | "unpublish" | "retract" | "archive" | "restoreArchive" | "reinstate";

const SENIOR_UP = ["SeniorEditor", "ManagingEditor", "EditorInChief"];
const CREATORS = ["Reporter", "AiEditor", "CopyEditor", ...SENIOR_UP];
const REVIEWERS = ["AiEditor", "CopyEditor", ...SENIOR_UP];

/** Arabic labels for the newsroom roles shown in the history (unknown roles fall back to their raw name). */
const ROLE_LABELS: Record<string, string> = {
  Reporter: "مراسل",
  AiEditor: "محرر الذكاء الاصطناعي",
  CopyEditor: "مدقق لغوي",
  SeniorEditor: "محرر أول",
  ManagingEditor: "مدير التحرير",
  EditorInChief: "رئيس التحرير",
  SystemAdmin: "مدير النظام",
  ScheduledPublisher: "النشر المجدول آليًا",
};

export function roleLabels(roles: string): string {
  return roles
    .split(",")
    .filter(Boolean)
    .map((r) => ROLE_LABELS[r] ?? r)
    .join("، ");
}

/** UI hint only (the API decides): who sees the "issue a correction" form — Senior, Managing, Editor-in-Chief. */
export function canIssueCorrection(roles: string[] | undefined): boolean {
  return !roles || roles.length === 0 || roles.some((r) => SENIOR_UP.includes(r));
}

/**
 * Which workflow buttons to SHOW for a role set and an article state. This is a
 * UI hint only — the API is the authority (it also applies ownership rules the
 * browser doesn't know) and answers 403/409 when an action isn't allowed. It
 * mirrors docs/decisions/PHASE3_EDITORIAL_DECISIONS.md (D1).
 */
export function workflowHints(roles: string[] | undefined, status: ArticleStatus): WorkflowAction[] {
  const has = (allowed: string[]) => !roles || roles.length === 0 || roles.some((r) => allowed.includes(r));
  const known = !!roles && roles.length > 0;
  const seniorUp = has(SENIOR_UP);
  const admin = known && roles!.includes("SystemAdmin");
  const chief = known && roles!.includes("EditorInChief");
  const actions: WorkflowAction[] = [];

  switch (status) {
    case "Draft":
      if (has(CREATORS)) actions.push("submit");
      if (!known || chief || admin) actions.push("publish"); // direct publishing / audited break-glass
      break;
    case "InReview":
      if (has(REVIEWERS)) actions.push("requestRevision");
      if (seniorUp) actions.push("approve");
      if (seniorUp || admin) actions.push("publish");
      break;
    case "Approved":
      if (seniorUp) actions.push("requestRevision");
      if (seniorUp || admin) actions.push("publish");
      if (seniorUp) actions.push("schedule");
      break;
    case "Scheduled":
      if (seniorUp) actions.push("cancelSchedule");
      break;
    case "Published":
      if (seniorUp || admin) actions.push("unpublish");
      if (seniorUp) actions.push("retract", "archive");
      break;
    case "Archived":
      if (seniorUp) actions.push("restoreArchive");
      break;
    case "Retracted":
      if (chief) actions.push("reinstate");
      break;
  }

  return actions;
}
