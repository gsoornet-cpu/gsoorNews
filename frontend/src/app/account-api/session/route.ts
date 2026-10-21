import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { backendUrl, forbiddenOrigin, isSameOrigin } from "@/lib/server/backend";

export const dynamic = "force-dynamic";
const USER_COOKIE = "jusoor_user_token";

export async function GET() {
  const token = (await cookies()).get(USER_COOKIE)?.value;
  if (!token) return NextResponse.json({ authenticated: false }, { status: 401 });
  const response = await fetch(backendUrl("/api/v1/profile"), { headers: { Authorization: `Bearer ${token}` }, cache: "no-store" }).catch(() => null);
  if (!response) return NextResponse.json({ message: "تعذّر الاتصال بالخادم." }, { status: 502 });
  if (!response.ok) return NextResponse.json({ authenticated: false }, { status: response.status });
  return NextResponse.json({ authenticated: true, profile: await response.json() });
}

export async function POST(request: Request) {
  if (!isSameOrigin(request)) return forbiddenOrigin();
  const body = await request.json().catch(() => null) as { action?: string; email?: string; password?: string; displayName?: string } | null;
  if (!body || !["login", "register"].includes(body.action ?? "") || !body.email || !body.password || (body.action === "register" && !body.displayName?.trim())) {
    return NextResponse.json({ message: "أكمل بيانات الحساب المطلوبة." }, { status: 400 });
  }
  const upstream = await fetch(backendUrl(`/api/v1/auth/${body.action}`), {
    method: "POST", headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body.action === "register" ? { email: body.email, password: body.password, displayName: body.displayName } : { email: body.email, password: body.password }),
    cache: "no-store",
  }).catch(() => null);
  if (!upstream) return NextResponse.json({ message: "تعذّر الاتصال بالخادم." }, { status: 502 });
  const result = await upstream.json().catch(() => ({})) as { accessToken?: string; expiresAtUtc?: string; message?: string; errors?: string[] };
  if (!upstream.ok || !result.accessToken || !result.expiresAtUtc) {
    const message = result.errors?.join(" ") || (body.action === "login" ? "تعذّر تسجيل الدخول. راجع البيانات وحاول مجددًا." : "تعذّر إنشاء الحساب. راجع البيانات وحاول مجددًا.");
    return NextResponse.json({ message }, { status: upstream.status });
  }
  const maxAge = Math.max(60, Math.floor((new Date(result.expiresAtUtc).getTime() - Date.now()) / 1000));
  const response = NextResponse.json({ ok: true });
  response.cookies.set(USER_COOKIE, result.accessToken, { httpOnly: true, sameSite: "strict", secure: process.env.NODE_ENV === "production" && process.env.COOKIE_INSECURE !== "true", path: "/", maxAge });
  return response;
}

export async function DELETE(request: Request) {
  if (!isSameOrigin(request)) return forbiddenOrigin();
  const response = NextResponse.json({ ok: true });
  response.cookies.delete(USER_COOKIE);
  return response;
}
