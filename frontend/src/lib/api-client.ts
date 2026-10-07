// Server-side rendering runs inside the web container, where the browser-facing
// address (localhost:5000) does not reach the API container. API_INTERNAL_BASE_URL
// (e.g. http://api:8080 in docker-compose) is used on the server when set; the
// browser always uses the public NEXT_PUBLIC_ value.
const PUBLIC_API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";
const API_BASE_URL =
  typeof window === "undefined" ? (process.env.API_INTERNAL_BASE_URL ?? PUBLIC_API_BASE_URL) : PUBLIC_API_BASE_URL;

export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
    public data?: unknown,
  ) {
    super(message);
  }
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...init?.headers,
    },
  });

  if (!response.ok) {
    // Never surface the raw response body to the caller uncritically —
    // the API's own ExceptionHandlingMiddleware already strips internals,
    // but this is the frontend's own boundary against leaking anything
    // unexpected into UI state.
    const data = response.status === 410 ? await response.json().catch(() => undefined) : undefined;
    throw new ApiError(`Request to ${path} failed with status ${response.status}`, response.status, data);
  }

  return (await response.json()) as T;
}
