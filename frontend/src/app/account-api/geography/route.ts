import { NextResponse } from "next/server";
import { backendUrl } from "@/lib/server/backend";

export const dynamic = "force-dynamic";

export async function GET(request: Request) {
  const countryId = new URL(request.url).searchParams.get("countryId");
  const path = countryId && /^[0-9a-f-]{36}$/i.test(countryId)
    ? `/api/v1/geography/countries/${countryId}/cities`
    : "/api/v1/geography/countries";
  const upstream = await fetch(backendUrl(path), { cache: "no-store" }).catch(() => null);
  if (!upstream) return NextResponse.json({ message: "تعذّر تحميل دليل البلدان والمدن." }, { status: 502 });
  const data = await upstream.json().catch(() => []);
  return NextResponse.json(data, { status: upstream.status });
}
