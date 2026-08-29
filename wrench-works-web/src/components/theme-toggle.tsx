"use client";

import { useTheme } from "@/hooks/use-theme";
import { Sun, Moon, Monitor } from "lucide-react";
import { cn } from "@/lib/utils";

const icons = {
  light: Sun,
  dark: Moon,
  system: Monitor,
};

const labels = {
  light: "Light",
  dark: "Dark",
  system: "System",
};

export function ThemeToggle({ collapsed }: { collapsed?: boolean }) {
  const { theme, toggle } = useTheme();
  const Icon = icons[theme];

  return (
    <button
      onClick={toggle}
      className={cn(
        "flex items-center gap-3 px-3 py-2 rounded-lg text-sm text-surface-500 hover:bg-surface-100 hover:text-surface-700 transition-colors w-full",
      )}
      title={`Theme: ${labels[theme]}`}
    >
      <Icon size={16} />
      {!collapsed && <span>{labels[theme]}</span>}
    </button>
  );
}
