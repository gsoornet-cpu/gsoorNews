import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { CMS_COOKIE, backendUrl, forbiddenOrigin, isSameOrigin } from "@/lib/server/backend";

export const dynamic = "force-dynamic";

/** Same-origin proxy for the homepage-video curation endpoint (GET list, PUT selection). */
async function proxy(request: Request) {
  if (request.method !== "GET" && !isSameOrigin(request)) return forbiddenOrigin();
  const token = (await cookies()).get(CMS_COOKIE)?.value;
  if (!token) return NextResponse.json({ message: "Unauthorized." }, { status: 401 });

  const body = request.method === "PUT" ? await request.text() : undefined;
  const upstream = await fetch(backendUrl("/api/v1/cms/home-videos"), {
    method: request.method,
    headers: { Authorization: `Bearer ${token}`, ...(body ? { "Content-Type": "application/json" } : {}) },
    body,
    cache: "no-store",
  }).catch(() => null);
  if (!upstream) return NextResponse.json({ message: "تعذّر الاتصال بالخادم." }, { status: 502 });

  const response = new NextResponse(upstream.status === 204 ? null : await upstream.text(), {
    status: upstream.status,
    headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" },
  });
  if (upstream.status === 401) response.cookies.delete(CMS_COOKIE);
  return response;
}

export { proxy as GET, proxy as PUT };
