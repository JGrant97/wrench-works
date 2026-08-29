import { NextRequest, NextResponse } from "next/server";
import { backendFetch } from "@/lib/proxy";
import { setSession } from "@/lib/session";

export async function POST(req: NextRequest) {
  const body = await req.json();

  const { data, status } = await backendFetch<{
    token: string;
    user: {
      id: string;
      name: string;
      email: string;
      businessId: string;
      businessName: string;
      permissions: string[];
    };
  }>("/api/auth/login", {
    method: "POST",
    body: JSON.stringify(body),
  });

  if (status !== 200) {
    return NextResponse.json(data, { status });
  }

  // Store JWT in httpOnly cookie, user info in readable cookie
  await setSession(data.token, data.user);

  // Return user info (without token) to the client
  return NextResponse.json({ user: data.user });
}
