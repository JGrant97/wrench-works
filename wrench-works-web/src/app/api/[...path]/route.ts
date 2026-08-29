import { NextRequest } from "next/server";
import { proxy } from "@/lib/proxy";

/**
 * Catch-all proxy route handler.
 *
 * Forwards any request to /api/* (that doesn't have a more specific
 * route handler) to the backend API. Specific route handlers like
 * /api/auth/login take precedence over this catch-all.
 *
 * The proxy automatically reads the JWT from the httpOnly cookie
 * and attaches it as a Bearer token to the backend request.
 */

export const GET = (req: NextRequest) => proxy(req);
export const POST = (req: NextRequest) => proxy(req);
export const PUT = (req: NextRequest) => proxy(req);
export const PATCH = (req: NextRequest) => proxy(req);
export const DELETE = (req: NextRequest) => proxy(req);
