import { NextResponse } from "next/server";
import { backendFetch } from "@/lib/proxy";
import { setSession, getToken } from "@/lib/session";

export async function POST() {
  const token = await getToken();
  if (!token) {
    return NextResponse.json({ code: "unauthorized" }, { status: 401 });
  }

  const { data, status } = await backendFetch<{
    token: string;
    user: Record<string, unknown>;
  }>("/api/auth/refresh", { method: "POST" });

  if (status !== 200) {
    return NextResponse.json(data, { status });
  }

  await setSession(data.token, data.user);
  return NextResponse.json({ user: data.user });
}
