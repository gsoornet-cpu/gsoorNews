import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { CMS_COOKIE, backendUrl } from "@/lib/server/backend";

export const dynamic = "force-dynamic";

export async function GET(request: Request) {
  const token = (await cookies()).get(CMS_COOKIE)?.value;
  if (!token) return NextResponse.json({ message: "Unauthorized." }, { status: 401 });
  const query = new URL(request.url).search;
  const upstream = await fetch(`${backendUrl("/api/v1/cms/seo/articles")}${query}`, {
    headers: { Authorization: `Bearer ${token}` }, cache: "no-store",
  }).catch(() => null);
  if (!upstream) return NextResponse.json({ message: "تعذّر الاتصال بالخادم." }, { status: 502 });
  return new NextResponse(await upstream.text(), { status: upstream.status, headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" } });
}
