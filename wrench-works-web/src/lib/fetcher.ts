/**
 * Client-side API helper.
 * All requests go to /api/* on the SAME origin (Next.js Route Handlers),
 * which proxy to the backend. The JWT is in an httpOnly cookie — the
 * browser never touches it directly.
 */

interface FieldError {
  field: string;
  message: string;
}

interface ApiErrorBody {
  code?: string;
  message?: string;
  /** FluentValidation failures. Present INSTEAD of `message` on a 400. */
  errors?: FieldError[];
  /** Extra context, e.g. { conflictingBookingIds: [...] } on a booking 409. */
  details?: unknown;
}

/**
 * Error thrown by every call in `fetcher`.
 *
 * The API's error middleware emits `message` for most exception types but validation
 * failures instead come back as `{ code: "validation_error", errors: [{ field, message }] }`
 * with no top-level `message`. Reading only `message` therefore turned every failed
 * form in the product into "Request failed with status 400" while the real, useful
 * text sat unread in `errors`.
 */
export class ApiError extends Error {
  status: number;
  data: unknown;
  /** Per-field validation failures, when the API sent any. */
  fieldErrors: FieldError[];
  /** Extra context the API attached, e.g. conflicting booking ids. */
  details: unknown;

  constructor(status: number, data: unknown) {
    const body = (typeof data === "object" && data !== null ? data : {}) as ApiErrorBody;
    const fieldErrors = Array.isArray(body.errors) ? body.errors : [];

    super(ApiError.buildMessage(status, body, fieldErrors));

    this.status = status;
    this.data = data;
    this.fieldErrors = fieldErrors;
    this.details = body.details;
  }

  private static buildMessage(status: number, body: ApiErrorBody, fieldErrors: FieldError[]): string {
    // Validation: surface the actual field messages, which is what the user needs.
    if (fieldErrors.length > 0) {
      return fieldErrors
        .map((e) => e.message)
        .filter(Boolean)
        .join("\n");
    }

    if (typeof body.message === "string" && body.message.length > 0) return body.message;

    return `Request failed with status ${status}`;
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
