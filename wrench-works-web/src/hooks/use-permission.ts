"use client";

import { useAuthStore } from "./use-auth";

export function usePermission(permission: string): boolean {
  const user = useAuthStore((s) => s.user);
  return user?.permissions?.includes(permission) ?? false;
}

export function useAnyPermission(permissions: string[]): boolean {
  const user = useAuthStore((s) => s.user);
  return permissions.some((p) => user?.permissions?.includes(p));
}
