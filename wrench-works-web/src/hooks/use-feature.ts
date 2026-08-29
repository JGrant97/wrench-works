"use client";

import { useAuthStore } from "./use-auth";

export function useFeature(feature: string): boolean {
  const user = useAuthStore((s) => s.user);
  return user?.features?.includes(feature) ?? false;
}

export function useFeatures(): string[] {
  const user = useAuthStore((s) => s.user);
  return user?.features ?? [];
}
