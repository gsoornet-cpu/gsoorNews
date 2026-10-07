import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { CMS_COOKIE, backendUrl, forbiddenOrigin, isSameOrigin } from "@/lib/server/backend";

export const dynamic = "force-dynamic";

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * Only these shapes are proxied — never an arbitrary backend path:
 *   ""  |  "{guid}"  |  "{guid}/{action}" where action is one of the
 *   workflow actions below (Slice 19, decision D1) or the history/corrections
 *   endpoints (Slice 20, decision D4)  |  "{guid}/revisions/{n}" (n = 1..9 digits).
 */
const ACTIONS = new Set(["submit", "request-revision", "approve", "publish", "schedule", "cancel-schedule", "unpublish", "history", "corrections"]);
const REVISION_NUMBER = /^[1-9][0-9]{0,8}$/;

function resolveTarget(segments: string[] | undefined): string | null {
  const parts = segments ?? [];
  if (parts.length === 0) return "/api/v1/cms/articles";
  if (!GUID.test(parts[0])) return null;
  if (parts.length === 1) return `/api/v1/cms/articles/${parts[0]}`;
  if (parts.length === 2 && ACTIONS.has(parts[1])) {
    return `/api/v1/cms/articles/${parts[0]}/${parts[1]}`;
  }
  if (parts.length === 2 && parts[1] === "seo") {
    return `/api/v1/cms/articles/${parts[0]}/seo`;
  }
  if (parts.length === 3 && parts[1] === "revisions" && REVISION_NUMBER.test(parts[2])) {
    return `/api/v1/cms/articles/${parts[0]}/revisions/${parts[2]}`;
  }
  return null;
}

async function proxy(request: Request, context: { params: Promise<{ path?: string[] }> }) {
  if (request.method !== "GET" && !isSameOrigin(request)) {
    return forbiddenOrigin();
  }

  const { path } = await context.params;
  const target = resolveTarget(path);
  if (!target) {
    return NextResponse.json({ message: "Not found." }, { status: 404 });
  }

  const token = (await cookies()).get(CMS_COOKIE)?.value;
  if (!token) {
    return NextResponse.json({ message: "Unauthorized." }, { status: 401 });
  }

  const search = new URL(request.url).search;
  const hasBody = request.method === "POST" || request.method === "PUT";
  const body = hasBody ? await request.text() : undefined;

  const upstream = await fetch(`${backendUrl(target)}${search}`, {
    method: request.method,
    headers: {
      Authorization: `Bearer ${token}`,
      ...(body ? { "Content-Type": "application/json" } : {}),
    },
    body: body || undefined,
    cache: "no-store",
  }).catch(() => null);

  if (!upstream) {
    return NextResponse.json({ message: "تعذّر الاتصال بالخادم." }, { status: 502 });
  }

  const response = new NextResponse(await upstream.text(), {
    status: upstream.status,
    headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" },
  });

  if (upstream.status === 401) {
    response.cookies.delete(CMS_COOKIE); // expired/invalid token: force a clean re-login
  }

  return response;
}

export { proxy as GET, proxy as POST, proxy as PUT };
