import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { CMS_COOKIE, backendUrl, forbiddenOrigin, isSameOrigin } from "@/lib/server/backend";

export const dynamic = "force-dynamic";

const ASSET_ID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function resolveTarget(path: string | undefined): string | null {
  if (!path || path === "") return "/api/v1/cms/media";
  if (path === "uploads") return "/api/v1/cms/media/uploads";
  const match = /^([0-9a-f-]{36})\/complete$/i.exec(path);
  if (match && ASSET_ID.test(match[1])) return `/api/v1/cms/media/${match[1]}/complete`;
  return null;
}

async function proxy(request: Request, context: { params: Promise<{ path?: string[] }> }) {
  if (request.method !== "GET" && !isSameOrigin(request)) return forbiddenOrigin();

  const { path } = await context.params;
  const target = resolveTarget(path?.join("/"));
  if (!target) return NextResponse.json({ message: "Not found." }, { status: 404 });

  const token = (await cookies()).get(CMS_COOKIE)?.value;
  if (!token) return NextResponse.json({ message: "Unauthorized." }, { status: 401 });

  const body = request.method === "POST" && target.endsWith("/uploads") ? await request.text() : undefined;
  const upstream = await fetch(`${backendUrl(target)}${new URL(request.url).search}`, {
    method: request.method,
    headers: {
      Authorization: `Bearer ${token}`,
      ...(body ? { "Content-Type": "application/json" } : {}),
    },
    body: body || undefined,
    cache: "no-store",
  }).catch(() => null);

  if (!upstream) return NextResponse.json({ message: "تعذّر الاتصال بالخادم." }, { status: 502 });
  const response = new NextResponse(await upstream.text(), {
    status: upstream.status,
    headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" },
  });
  if (upstream.status === 401) response.cookies.delete(CMS_COOKIE);
  return response;
}

export { proxy as GET, proxy as POST };
