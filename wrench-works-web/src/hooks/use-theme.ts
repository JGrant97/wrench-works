"use client";

import { useEffect, useCallback } from "react";
import { create } from "zustand";

type Theme = "light" | "dark" | "system";

interface ThemeState {
  theme: Theme;
  resolved: "light" | "dark";
  setTheme: (theme: Theme) => void;
}

function getSystemTheme(): "light" | "dark" {
  if (typeof window === "undefined") return "light";
  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

function resolve(theme: Theme): "light" | "dark" {
  return theme === "system" ? getSystemTheme() : theme;
}

function applyTheme(resolved: "light" | "dark") {
  const root = document.documentElement;
  root.classList.toggle("dark", resolved === "dark");
}

export const useThemeStore = create<ThemeState>((set) => ({
  theme: "system",
  resolved: "light",
  setTheme: (theme) => {
    const resolved = resolve(theme);
    localStorage.setItem("ww_theme", theme);
    applyTheme(resolved);
    set({ theme, resolved });
  },
}));

/**
 * Hydrate theme from localStorage on mount + listen for system changes.
 * Call this once in the root layout.
 */
export function useThemeInit() {
  const setTheme = useThemeStore((s) => s.setTheme);

  useEffect(() => {
    const stored = localStorage.getItem("ww_theme") as Theme | null;
    const theme = stored ?? "system";
    const resolved = resolve(theme);
    applyTheme(resolved);
    useThemeStore.setState({ theme, resolved });

    // Listen for OS theme changes when set to "system"
    const mq = window.matchMedia("(prefers-color-scheme: dark)");
    const handler = () => {
      const current = useThemeStore.getState().theme;
      if (current === "system") {
        const resolved = getSystemTheme();
        applyTheme(resolved);
        useThemeStore.setState({ resolved });
      }
    };
    mq.addEventListener("change", handler);
    return () => mq.removeEventListener("change", handler);
  }, [setTheme]);
}

export function useTheme() {
  const { theme, resolved, setTheme } = useThemeStore();

  const toggle = useCallback(() => {
    // Cycle: light → dark → system → light
    const next: Record<Theme, Theme> = { light: "dark", dark: "system", system: "light" };
    setTheme(next[theme]);
  }, [theme, setTheme]);

  return { theme, resolved, setTheme, toggle };
}
