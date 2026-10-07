import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { CMS_COOKIE, backendUrl, forbiddenOrigin, isSameOrigin } from "@/lib/server/backend";

export const dynamic = "force-dynamic";
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

async function proxy(request: Request, context: { params: Promise<{ path?: string[] }> }) {
  if (request.method !== "GET" && !isSameOrigin(request)) return forbiddenOrigin();
  const { path = [] } = await context.params;
  if (path.length > 1 || (path.length === 1 && !GUID.test(path[0]))) return NextResponse.json({ message: "Not found." }, { status: 404 });
  const allowed = path.length === 0 ? ["GET", "POST"] : ["PUT", "DELETE"];
  if (!allowed.includes(request.method)) return NextResponse.json({ message: "Method not allowed." }, { status: 405 });
  const token = (await cookies()).get(CMS_COOKIE)?.value;
  if (!token) return NextResponse.json({ message: "Unauthorized." }, { status: 401 });
  const url = `/api/v1/cms/editorial-categories${path.length ? `/${path[0]}` : ""}`;
  const body = request.method === "POST" || request.method === "PUT" ? await request.text() : undefined;
  const upstream = await fetch(backendUrl(url), { method: request.method, headers: { Authorization: `Bearer ${token}`, ...(body ? { "Content-Type": "application/json" } : {}) }, body, cache: "no-store" }).catch(() => null);
  if (!upstream) return NextResponse.json({ message: "تعذّر الاتصال بالخادم." }, { status: 502 });
  return new NextResponse(await upstream.text(), { status: upstream.status, headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" } });
}

export { proxy as GET, proxy as POST, proxy as PUT, proxy as DELETE };
