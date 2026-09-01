"use client";

import { useCallback, useEffect } from "react";
import { create } from "zustand";
import { useRouter } from "next/navigation";
import { fetcher } from "@/lib/fetcher";

export interface AuthUser {
  id: string;
  name: string;
  email: string;
  businessId: string;
  businessName: string;
  /** The business's display currency, from /settings/general. See lib/currency.ts. */
  currency?: string;
  permissions: string[];
  features: string[];
}

interface AuthState {
  user: AuthUser | null;
  isLoading: boolean;
  setUser: (user: AuthUser | null) => void;
  setLoading: (loading: boolean) => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  isLoading: true,
  setUser: (user) => set({ user, isLoading: false }),
  setLoading: (isLoading) => set({ isLoading }),
}));

/**
 * Reads the ww_user cookie (non-httpOnly) to hydrate user state.
 * The actual JWT stays in the httpOnly ww_token cookie.
 */
function readUserCookie(): AuthUser | null {
  if (typeof document === "undefined") return null;
  const match = document.cookie
    .split("; ")
    .find((c) => c.startsWith("ww_user="));
  if (!match) return null;
  try {
    return JSON.parse(decodeURIComponent(match.split("=").slice(1).join("=")));
  } catch {
    return null;
  }
}

export function useAuth() {
  const { user, isLoading, setUser, setLoading } = useAuthStore();
  const router = useRouter();

  // Hydrate from cookie on mount
  useEffect(() => {
    const cookieUser = readUserCookie();
    setUser(cookieUser);
  }, [setUser]);

  const login = useCallback(
    async (email: string, password: string) => {
      setLoading(true);
      try {
        const data = await fetcher.post<{ user: AuthUser }>("/api/auth/login", {
          email,
          password,
        });
        setUser(data.user);
        router.push("/calendar");
      } finally {
        setLoading(false);
      }
    },
    [setUser, setLoading, router]
  );

  const register = useCallback(
    async (payload: {
      businessName: string;
      ownerName: string;
      email: string;
      password: string;
    }) => {
      const data = await fetcher.post<{ userId: string; businessId: string }>(
        "/api/auth/register",
        payload
      );
      return data;
    },
    []
  );

  const logout = useCallback(async () => {
    await fetcher.post("/api/auth/logout");
    setUser(null);
    router.push("/login");
  }, [setUser, router]);

  const hasPermission = useCallback(
    (permission: string) => user?.permissions?.includes(permission) ?? false,
    [user]
  );

  const hasFeature = useCallback(
    (feature: string) => user?.features?.includes(feature) ?? false,
    [user]
  );

  return {
    user,
    isLoading,
    isAuthenticated: !!user,
    login,
    register,
    logout,
    hasPermission,
    hasFeature,
  };
}
