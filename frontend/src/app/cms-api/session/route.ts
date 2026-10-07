import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { CMS_COOKIE, backendUrl, decodeTokenClaims, forbiddenOrigin, isSameOrigin } from "@/lib/server/backend";

export const dynamic = "force-dynamic";

/** Who am I? Used by the CMS UI to decide what to show. */
export async function GET() {
  const token = (await cookies()).get(CMS_COOKIE)?.value;
  if (!token) {
    return NextResponse.json({ authenticated: false }, { status: 401 });
  }

  const { email, roles, exp } = decodeTokenClaims(token);
  if (exp !== null && exp * 1000 < Date.now()) {
    return NextResponse.json({ authenticated: false }, { status: 401 });
  }

  return NextResponse.json({ authenticated: true, email, roles });
}

/** Login: exchanges credentials for the API's JWT and keeps it in an httpOnly cookie. */
export async function POST(request: Request) {
  if (!isSameOrigin(request)) {
    return forbiddenOrigin();
  }

  const credentials = (await request.json().catch(() => null)) as { email?: string; password?: string } | null;
  if (!credentials?.email || !credentials?.password) {
    return NextResponse.json({ message: "البريد الإلكتروني وكلمة المرور مطلوبان." }, { status: 400 });
  }

  const upstream = await fetch(backendUrl("/api/v1/auth/login"), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email: credentials.email, password: credentials.password }),
  }).catch(() => null);

  if (!upstream) {
    return NextResponse.json({ message: "تعذّر الاتصال بالخادم." }, { status: 502 });
  }

  if (!upstream.ok) {
    return NextResponse.json({ message: "بيانات الدخول غير صحيحة." }, { status: upstream.status === 401 ? 401 : 502 });
  }

  const tokens = (await upstream.json()) as { accessToken: string; expiresAtUtc: string };
  const maxAge = Math.max(60, Math.floor((new Date(tokens.expiresAtUtc).getTime() - Date.now()) / 1000));

  const response = NextResponse.json({ ok: true });
  response.cookies.set(CMS_COOKIE, tokens.accessToken, {
    httpOnly: true,
    sameSite: "strict",
    secure: process.env.NODE_ENV === "production" && process.env.COOKIE_INSECURE !== "true",
    path: "/",
    maxAge,
  });
  return response;
}

export async function DELETE(request: Request) {
  if (!isSameOrigin(request)) {
    return forbiddenOrigin();
  }

  const response = NextResponse.json({ ok: true });
  response.cookies.delete(CMS_COOKIE);
  return response;
}
