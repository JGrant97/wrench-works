"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";
import { useAuthStore } from "@/hooks/use-auth";
import { Building2, Grid3X3, Users, CreditCard } from "lucide-react";

const SETTINGS_ITEMS = [
  { href: "/settings/general", icon: Building2, label: "General", permission: null as string | null },
  { href: "/settings/zones", icon: Grid3X3, label: "Zones / Bays", permission: "settings.manage" },
  { href: "/settings/users", icon: Users, label: "Users & Roles", permission: "users.manage" },
  { href: "/settings/billing", icon: CreditCard, label: "Billing", permission: "billing.manage" },
];

export function SettingsNav() {
  const pathname = usePathname();
  const permissions = useAuthStore((s) => s.user?.permissions ?? []);

  return (
    <nav className="w-48 flex-shrink-0 space-y-1">
      {SETTINGS_ITEMS.filter(
        (item) => !item.permission || permissions.includes(item.permission)
      ).map((item) => {
        const active = pathname === item.href;
        return (
          <Link
            key={item.href}
            href={item.href}
            className={cn(
              "flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm transition-colors",
              active ? "bg-brand-50 text-brand-700 font-medium dark:bg-brand-950/40 dark:text-brand-400" : "text-surface-500 hover:bg-surface-100 hover:text-surface-700"
            )}
          >
            <item.icon size={16} />
            {item.label}
          </Link>
        );
      })}
    </nav>
  );
}
