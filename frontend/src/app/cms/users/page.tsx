"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { CmsError, cms } from "@/lib/cms-client";

type NewsroomRole = "Reporter" | "Researcher" | "AiEditor" | "CopyEditor" | "SeniorEditor" | "ManagingEditor" | "EditorInChief" | "FactChecker" | "SeoEditor" | "CrisisEditor" | "SystemAdmin";
type UserRow = { id: string; email: string | null; displayName: string; roles: NewsroomRole[] };
type UsersPage = { items: UserRow[]; page: number; pageSize: number; totalCount: number; totalPages: number };
const ROLE_NAMES: Record<NewsroomRole, string> = { Reporter: "مراسل", Researcher: "باحث", AiEditor: "محرر ذكاء اصطناعي", CopyEditor: "مدقق لغوي", SeniorEditor: "محرر أول", ManagingEditor: "مدير التحرير", EditorInChief: "رئيس التحرير", FactChecker: "مدقق حقائق", SeoEditor: "محرر SEO", CrisisEditor: "محرر اطمّن", SystemAdmin: "مدير النظام" };
const ROLES = Object.keys(ROLE_NAMES) as NewsroomRole[];

async function usersApi<T>(path: string, method = "GET", body?: unknown): Promise<T> {
  const response = await fetch(`/cms-api/admin-users${path}`, { method, credentials: "same-origin", cache: "no-store", headers: body ? { "Content-Type": "application/json" } : undefined, body: body ? JSON.stringify(body) : undefined });
  if (!response.ok) {
    const data = await response.json().catch(() => ({})) as { message?: string };
    throw new CmsError(data.message ?? (response.status === 403 ? "ليست لديك صلاحية إدارة المستخدمين." : "تعذر تنفيذ الطلب."), response.status);
  }
  return response.json() as Promise<T>;
}

export default function CmsUsersPage() {
  const router = useRouter();
  const [data, setData] = useState<UsersPage | null>(null);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [canManage, setCanManage] = useState(false);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const load = useCallback(async () => {
    setLoading(true); setError(null);
    try {
      const session = await cms.session();
      if (!session.authenticated) { router.replace("/cms/login"); return; }
      setCanManage(session.roles?.includes("SystemAdmin") ?? false);
      const query = new URLSearchParams({ page: String(page), pageSize: "20" });
      if (search.trim()) query.set("search", search.trim());
      setData(await usersApi<UsersPage>(`?${query}`));
    } catch (e) {
      if (e instanceof CmsError && e.status === 401) router.replace("/cms/login");
      else setError(e instanceof Error ? e.message : "تعذر تحميل المستخدمين.");
    } finally { setLoading(false); }
  }, [page, router, search]);
  useEffect(() => { const timer = window.setTimeout(() => void load(), search ? 250 : 0); return () => window.clearTimeout(timer); }, [load, search]);

  async function changeRole(user: UserRow, role: NewsroomRole, action: "grant" | "revoke") {
    const key = `${user.id}:${role}`; setBusy(key); setError(null); setNotice(null);
    try {
      await usersApi(`/${encodeURIComponent(user.id)}/roles${action === "revoke" ? `/${role}` : ""}`, action === "grant" ? "POST" : "DELETE", action === "grant" ? { role } : undefined);
      setNotice(action === "grant" ? `تم منح صلاحية ${ROLE_NAMES[role]} للمستخدم.` : `تم سحب صلاحية ${ROLE_NAMES[role]} من المستخدم.`);
      await load();
    } catch (e) { setError(e instanceof Error ? e.message : "تعذر تحديث الصلاحيات."); }
    finally { setBusy(null); }
  }

  return <div className="cms-page-stack">
    <section className="cms-page-heading"><div><span className="cms-eyebrow">إدارة المنصة</span><h1>المستخدمون والصلاحيات</h1><p>ابحث في حسابات هيئة التحرير وأدر الأدوار حسب صلاحيتك.</p></div><span className="cms-heading-icon" aria-hidden="true">♙</span></section>
    <section className="cms-card cms-users-card">
      <div className="cms-users-toolbar"><label className="cms-news-search"><span aria-hidden="true">⌕</span><input type="search" maxLength={100} value={search} onChange={(event) => { setPage(1); setSearch(event.target.value); }} placeholder="ابحث بالاسم أو البريد الإلكتروني" aria-label="البحث عن مستخدم" /></label><span className="cms-hint" aria-live="polite">{loading ? "جارٍ التحميل…" : `${data?.totalCount ?? 0} حساب`}</span></div>
      {error && <div className="cms-msg error" role="alert">{error}</div>}{notice && <div className="cms-msg ok" role="status">{notice}</div>}
      {loading && !data ? <p>جارٍ تحميل المستخدمين…</p> : data?.items.length ? <div className="cms-table-scroll"><table className="cms-table"><thead><tr><th>المستخدم</th><th>الأدوار الحالية</th><th>إدارة الدور</th></tr></thead><tbody>{data.items.map((user) => <tr key={user.id}><td data-label="المستخدم"><strong>{user.displayName}</strong><small className="cms-user-email" dir="ltr">{user.email ?? "—"}</small></td><td data-label="الأدوار الحالية"><div className="cms-role-chips">{user.roles.length ? user.roles.map((role) => <span className="cms-role-chip" key={role}>{ROLE_NAMES[role] ?? role}{canManage && <button type="button" aria-label={`سحب ${ROLE_NAMES[role]}`} disabled={busy === `${user.id}:${role}`} onClick={() => { if (window.confirm(`هل تريد سحب دور ${ROLE_NAMES[role]} من ${user.displayName}؟`)) void changeRole(user, role, "revoke"); }}>×</button>}</span>) : <span className="cms-hint">لا توجد أدوار</span>}</div></td><td data-label="إدارة الدور">{canManage ? <GrantRoleControl user={user} onGrant={(role) => changeRole(user, role, "grant")} busy={busy} /> : <span className="cms-hint">عرض فقط</span>}</td></tr>)}</tbody></table></div> : <div className="cms-news-empty">{search ? "لا توجد حسابات تطابق البحث." : "لا توجد حسابات هيئة تحرير لعرضها."}</div>}
      {data && data.totalPages > 1 && <div className="cms-pager"><button className="cms-btn" disabled={page <= 1 || loading} onClick={() => setPage((value) => Math.max(1, value - 1))}>السابق</button><span>{data.page} من {data.totalPages}</span><button className="cms-btn" disabled={page >= data.totalPages || loading} onClick={() => setPage((value) => Math.min(data.totalPages, value + 1))}>التالي</button></div>}
    </section>
    <p className="cms-hint cms-security-note">تغيير الأدوار مسجل في سجل التدقيق، والخادم يطبق الصلاحيات ويمنع إزالة آخر مدير نظام.</p>
  </div>;
}

function GrantRoleControl({ user, onGrant, busy }: { user: UserRow; onGrant: (role: NewsroomRole) => void; busy: string | null }) {
  const [selected, setSelected] = useState<NewsroomRole | "">("");
  const options = ROLES.filter((role) => !user.roles.includes(role));
  return <div className="cms-grant-role"><select aria-label={`اختيار دور لـ ${user.displayName}`} value={selected} onChange={(event) => setSelected(event.target.value as NewsroomRole | "")}><option value="">اختر دورًا…</option>{options.map((role) => <option key={role} value={role}>{ROLE_NAMES[role]}</option>)}</select><button className="cms-btn primary" type="button" disabled={!selected || busy === `${user.id}:${selected}`} onClick={() => { if (selected) onGrant(selected); }}>منح</button></div>;
}
