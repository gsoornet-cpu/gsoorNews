import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { backendUrl, forbiddenOrigin, isSameOrigin } from "@/lib/server/backend";

export const dynamic = "force-dynamic";
const USER_COOKIE = "jusoor_user_token";

async function tokenOrUnauthorized() {
  const token = (await cookies()).get(USER_COOKIE)?.value;
  return token ?? null;
}

export async function POST(request: Request) {
  if (!isSameOrigin(request)) return forbiddenOrigin();
  const token = await tokenOrUnauthorized();
  if (!token) return NextResponse.json({ message: "سجّل الدخول أولًا." }, { status: 401 });
  const payload = await request.json().catch(() => null) as { cityId?: string } | null;
  if (!payload?.cityId || !/^[0-9a-f-]{36}$/i.test(payload.cityId)) return NextResponse.json({ message: "اختر مدينة صحيحة." }, { status: 400 });
  const upstream = await fetch(backendUrl("/api/v1/profile/places"), { method: "POST", headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" }, body: JSON.stringify({ cityId: payload.cityId }), cache: "no-store" }).catch(() => null);
  if (!upstream) return NextResponse.json({ message: "تعذّر الاتصال بالخادم." }, { status: 502 });
  if (!upstream.ok) return NextResponse.json({ message: "تعذّر إضافة المدينة إلى قائمة المتابعة." }, { status: upstream.status });
  return NextResponse.json({ ok: true });
}

export async function DELETE(request: Request) {
  if (!isSameOrigin(request)) return forbiddenOrigin();
  const token = await tokenOrUnauthorized();
  if (!token) return NextResponse.json({ message: "سجّل الدخول أولًا." }, { status: 401 });
  const cityId = new URL(request.url).searchParams.get("cityId");
  if (!cityId || !/^[0-9a-f-]{36}$/i.test(cityId)) return NextResponse.json({ message: "اختر مدينة صحيحة." }, { status: 400 });
  const upstream = await fetch(backendUrl(`/api/v1/profile/places/${cityId}`), { method: "DELETE", headers: { Authorization: `Bearer ${token}` }, cache: "no-store" }).catch(() => null);
  if (!upstream) return NextResponse.json({ message: "تعذّر الاتصال بالخادم." }, { status: 502 });
  if (!upstream.ok) return NextResponse.json({ message: "تعذّر إزالة المدينة من قائمة المتابعة." }, { status: upstream.status });
  return NextResponse.json({ ok: true });
}
