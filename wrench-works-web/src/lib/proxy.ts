import { NextRequest, NextResponse } from "next/server";
import { getToken } from "./session";

const API_BASE_URL = process.env.API_BASE_URL ?? "http://localhost:5000";

type ProxyOptions = {
  /** Override the backend path (defaults to stripping /api prefix) */
  backendPath?: string;
  /** Skip auth token forwarding */
  noAuth?: boolean;
};

/**
 * Generic proxy for Next.js Route Handlers → Backend API.
 *
 * Reads JWT from the httpOnly cookie and forwards the request to the
 * .NET backend. Use this for simple pass-through routes. For routes
 * that need transformation, use the Orval-generated client directly.
 *
 * Usage in a route handler:
 *   export const GET = (req: NextRequest) => proxy(req);
 *   export const POST = (req: NextRequest) => proxy(req);
 */
export async function proxy(
  req: NextRequest,
  opts?: ProxyOptions
): Promise<NextResponse> {
  const url = new URL(req.url);

  // Map /api/xyz → /api/xyz on the backend (same paths)
  const backendPath = opts?.backendPath ?? url.pathname;
  const backendUrl = `${API_BASE_URL}${backendPath}${url.search}`;

  const headers: Record<string, string> = {
    "Content-Type": req.headers.get("content-type") ?? "application/json",
  };

  if (!opts?.noAuth) {
    const token = await getToken();
    if (token) {
      headers["Authorization"] = `Bearer ${token}`;
    }
  }

  try {
    const body =
      req.method !== "GET" && req.method !== "HEAD"
        ? await req.text()
        : undefined;

    const response = await fetch(backendUrl, {
      method: req.method,
      headers,
      body,
    });

    // Handle 204 No Content and other empty responses
    if (response.status === 204 || response.headers.get("content-length") === "0") {
      return new NextResponse(null, { status: response.status });
    }

    const contentType = response.headers.get("content-type") ?? "";
    const isJson = contentType.includes("application/json");

    if (isJson) {
      const data = await response.json();
      return NextResponse.json(data, { status: response.status });
    }

    const text = await response.text();
    if (!text) {
      return new NextResponse(null, { status: response.status });
    }

    return NextResponse.json(text, { status: response.status });
  } catch (error) {
    console.error(`Proxy error: ${req.method} ${backendPath}`, error);
    return NextResponse.json(
      { code: "proxy_error", message: "Failed to reach backend" },
      { status: 502 }
    );
  }
}

/**
 * Typed backend fetch — use when you need to call the backend from a
 * route handler and transform the response, rather than pass-through.
 */
export async function backendFetch<T = unknown>(
  path: string,
  init?: RequestInit
): Promise<{ data: T; status: number }> {
  const token = await getToken();
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...(init?.headers as Record<string, string>),
  };

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers,
  });

  const data = (await response.json().catch(() => null)) as T;
  return { data, status: response.status };
}
