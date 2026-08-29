import { NextRequest, NextResponse } from "next/server";
import { backendFetch } from "@/lib/proxy";

export async function POST(req: NextRequest) {
  const body = await req.json();

  const { data, status } = await backendFetch("/api/auth/register", {
    method: "POST",
    body: JSON.stringify(body),
  });

  return NextResponse.json(data, { status });
}
