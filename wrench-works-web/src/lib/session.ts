import { cookies } from "next/headers";

const TOKEN_COOKIE = "ww_token";
const USER_COOKIE = "ww_user";

const COOKIE_OPTIONS = {
  httpOnly: true,
  secure: process.env.NODE_ENV === "production",
  sameSite: "lax" as const,
  path: "/",
  maxAge: 60 * 60 * 24, // 24 hours
};

export async function setSession(token: string, user: Record<string, unknown>) {
  const cookieStore = await cookies();
  cookieStore.set(TOKEN_COOKIE, token, COOKIE_OPTIONS);
  // User info in a non-httpOnly cookie so the client can read it
  cookieStore.set(USER_COOKIE, JSON.stringify(user), {
    ...COOKIE_OPTIONS,
    httpOnly: false,
  });
}

export async function clearSession() {
  const cookieStore = await cookies();
  cookieStore.delete(TOKEN_COOKIE);
  cookieStore.delete(USER_COOKIE);
}

export async function getToken(): Promise<string | undefined> {
  const cookieStore = await cookies();
  return cookieStore.get(TOKEN_COOKIE)?.value;
}

export async function getSessionUser(): Promise<Record<string, unknown> | null> {
  const cookieStore = await cookies();
  const raw = cookieStore.get(USER_COOKIE)?.value;
  if (!raw) return null;
  try {
    return JSON.parse(raw);
  } catch {
    return null;
  }
}
