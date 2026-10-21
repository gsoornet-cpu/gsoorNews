import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { backendUrl, forbiddenOrigin, isSameOrigin } from "@/lib/server/backend";

export const dynamic = "force-dynamic";
const USER_COOKIE = "jusoor_user_token";

export async function PUT(request: Request) {
  if (!isSameOrigin(request)) return forbiddenOrigin();
  const token = (await cookies()).get(USER_COOKIE)?.value;
  if (!token) return NextResponse.json({ message: "سجّل الدخول أولًا." }, { status: 401 });
  const payload = await request.json().catch(() => null);
  if (!payload || typeof payload.privacyLevel !== "string") return NextResponse.json({ message: "بيانات الموقع غير مكتملة." }, { status: 400 });
  const upstream = await fetch(backendUrl("/api/v1/profile/location"), { method: "PUT", headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" }, body: JSON.stringify(payload), cache: "no-store" }).catch(() => null);
  if (!upstream) return NextResponse.json({ message: "تعذّر الاتصال بالخادم." }, { status: 502 });
  if (!upstream.ok) {
    const error = await upstream.json().catch(() => ({})) as { message?: string };
    return NextResponse.json({ message: error.message ?? "تعذّر حفظ الموقع. تحقق من الحقول المختارة." }, { status: upstream.status });
  }
  return NextResponse.json({ ok: true });
}
