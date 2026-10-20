import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { CMS_COOKIE, backendUrl, forbiddenOrigin, isSameOrigin } from "@/lib/server/backend";

export const dynamic = "force-dynamic";
type RouteContext = { params: Promise<{ path?: string[] }> };

async function proxy(request: Request, context: RouteContext) {
  const path = (await context.params).path ?? [];
  if (path.length > 1 || (path[0] && !/^[0-9a-f-]{36}$/i.test(path[0]))) return NextResponse.json({ message: "Not found." }, { status: 404 });
  if (request.method !== "GET" && !isSameOrigin(request)) return forbiddenOrigin();
  const token = (await cookies()).get(CMS_COOKIE)?.value;
  if (!token) return NextResponse.json({ message: "Unauthorized." }, { status: 401 });
  const body = request.method === "POST" || request.method === "PUT" ? await request.text() : undefined;
  const upstream = await fetch(backendUrl(`/api/v1/cms/diaspora-estimates${path.length ? `/${path[0]}` : ""}`), {
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

export { proxy as GET, proxy as POST, proxy as PUT, proxy as DELETE };
