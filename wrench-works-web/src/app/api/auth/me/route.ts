import { NextResponse } from "next/server";
import { backendFetch } from "@/lib/proxy";

export async function GET() {
  const { data, status } = await backendFetch("/api/users/me");
  return NextResponse.json(data, { status });
}
