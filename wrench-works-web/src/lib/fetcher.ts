/**
 * Client-side API helper.
 * All requests go to /api/* on the SAME origin (Next.js Route Handlers),
 * which proxy to the backend. The JWT is in an httpOnly cookie — the
 * browser never touches it directly.
 */

export class ApiError extends Error {
  status: number;
  data: unknown;

  constructor(status: number, data: unknown) {
    super(typeof data === "object" && data !== null && "message" in data
      ? (data as { message: string }).message
      : `Request failed with status ${status}`);
    this.status = status;
    this.data = data;
  }
}

async function handleResponse<T>(res: Response): Promise<T> {
  const data = await res.json().catch(() => null);
  if (!res.ok) throw new ApiError(res.status, data);
  return data as T;
}

export const fetcher = {
  get: <T = unknown>(url: string, params?: Record<string, string>) => {
    const qs = params ? `?${new URLSearchParams(params).toString()}` : "";
    return fetch(`${url}${qs}`, { credentials: "include" }).then((r) =>
      handleResponse<T>(r)
    );
  },

  post: <T = unknown>(url: string, body?: unknown) =>
    fetch(url, {
      method: "POST",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
      body: body ? JSON.stringify(body) : undefined,
    }).then((r) => handleResponse<T>(r)),

  put: <T = unknown>(url: string, body?: unknown) =>
    fetch(url, {
      method: "PUT",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
      body: body ? JSON.stringify(body) : undefined,
    }).then((r) => handleResponse<T>(r)),

  patch: <T = unknown>(url: string, body?: unknown) =>
    fetch(url, {
      method: "PATCH",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
      body: body ? JSON.stringify(body) : undefined,
    }).then((r) => handleResponse<T>(r)),

  delete: <T = unknown>(url: string) =>
    fetch(url, {
      method: "DELETE",
      credentials: "include",
    }).then((r) => handleResponse<T>(r)),
};

/**
 * SWR-compatible fetcher. Usage:
 *   const { data } = useSWR('/api/customers', swrFetcher);
 */
export const swrFetcher = <T = unknown>(url: string): Promise<T> =>
  fetcher.get<T>(url);
