import { NextResponse } from "next/server";
import { apiFetch } from "@/lib/api-client";
import type { StorySummary, PagedResult } from "@/lib/stories-api";

export const dynamic = "force-dynamic";

export async function GET(request: Request) {
  const url = new URL(request.url);
  const q = url.searchParams.get("q")?.trim() ?? "";
  const limit = Math.min(10, Math.max(1, Number(url.searchParams.get("limit")) || 5));
  if (q.length < 2 || q.length > 120) return NextResponse.json({ items: [], totalCount: 0 });

  try {
    const query = new URLSearchParams({ q, page: "1", pageSize: String(limit) });
    const result = await apiFetch<PagedResult<StorySummary>>(`/api/v1/news?${query}` , { cache: "no-store" });
    return NextResponse.json(result, { headers: { "Cache-Control": "no-store" } });
  } catch {
    return NextResponse.json({ message: "تعذّر البحث مؤقتًا." }, { status: 502 });
  }
}
