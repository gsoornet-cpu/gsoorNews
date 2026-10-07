// Server-only helpers for the CMS session proxy (BFF).
//
// The JWT never reaches browser JavaScript: the login route stores it in an
// httpOnly, SameSite=Strict cookie and the proxy attaches it as a Bearer
// header server-side. That removes the XSS-steals-token risk of keeping it in
// localStorage, and SameSite=Strict + the Origin check below covers CSRF for
// the cookie-authenticated proxy.
import { NextResponse } from "next/server";

export const CMS_COOKIE = "jusoor_cms_token";

export function backendUrl(path: string): string {
  const base =
    process.env.API_INTERNAL_BASE_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";
  return `${base}${path}`;
}

/** Rejects cross-site state-changing requests. Browsers always send Origin on POST/PUT. */
export function isSameOrigin(request: Request): boolean {
  const origin = request.headers.get("origin");
  if (!origin) {
    return false;
  }

  const host = request.headers.get("x-forwarded-host") ?? request.headers.get("host");

  try {
    return host !== null && new URL(origin).host === host;
  } catch {
    return false;
  }
}

export function forbiddenOrigin(): NextResponse {
  return NextResponse.json({ message: "Cross-origin request rejected." }, { status: 403 });
}

/** UI hints only — the API re-validates the token and enforces every permission. */
export function decodeTokenClaims(token: string): { email: string | null; roles: string[]; exp: number | null } {
  try {
    const payload = JSON.parse(Buffer.from(token.split(".")[1], "base64url").toString("utf-8")) as Record<string, unknown>;
    const roleClaim =
      payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] ?? payload["role"] ?? payload["roles"];
    const roles = Array.isArray(roleClaim) ? roleClaim.map(String) : roleClaim ? [String(roleClaim)] : [];
    const email = typeof payload["email"] === "string" ? (payload["email"] as string) : null;
    const exp = typeof payload["exp"] === "number" ? (payload["exp"] as number) : null;
    return { email, roles, exp };
  } catch {
    return { email: null, roles: [], exp: null };
  }
}
