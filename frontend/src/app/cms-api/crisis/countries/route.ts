import { NextResponse } from "next/server";
import { backendUrl } from "@/lib/server/backend";

export const dynamic = "force-dynamic";

/** Same-origin BFF for the public country catalog used by the CMS crisis editor. */
export async function GET() {
  const upstream = await fetch(backendUrl("/api/v1/geography/countries"), { cache: "no-store" }).catch(() => null);
  if (!upstream) return NextResponse.json({ message: "تعذّر الاتصال بخدمة دليل الدول." }, { status: 502 });

  return new NextResponse(await upstream.text(), {
    status: upstream.status,
    headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" },
  });
}
