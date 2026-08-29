"use client";

import { useEffect } from "react";
import { useRouter, usePathname } from "next/navigation";
import Link from "next/link";
import { useAuth } from "@/hooks/use-auth";
import { usePermission } from "@/hooks/use-permission";
import { useFeature } from "@/hooks/use-feature";
import { cn } from "@/lib/utils";
import { Spinner } from "@/components/ui";
import { ThemeToggle } from "@/components/theme-toggle";
import {
  Calendar,
  Wrench as WrenchIcon,
  Users,
  Car,
  Package,
  Settings,
  LogOut,
  Briefcase,
  ChevronLeft,
} from "lucide-react";

const NAV_ITEMS = [
  { href: "/calendar", icon: Calendar, label: "Calendar", permission: "calendar.view", feature: null as string | null },
  { href: "/jobs", icon: Briefcase, label: "Jobs", permission: "jobs.view", feature: null },
  { href: "/customers", icon: Users, label: "Customers", permission: "customers.view", feature: null },
  { href: "/vehicles", icon: Car, label: "Vehicles", permission: "vehicles.view", feature: null },
  { href: "/inventory", icon: Package, label: "Inventory", permission: "inventory.view", feature: "inventory" },
  { href: "/settings/general", icon: Settings, label: "Settings", permission: null, feature: null },
];

function NavLink({ href, icon: Icon, label, permission, feature }: typeof NAV_ITEMS[number]) {
  const pathname = usePathname();
  const hasPermission = usePermission(permission ?? "");
  const hasFeature = useFeature(feature ?? "");
  const permissionOk = permission ? hasPermission : true;
  const featureOk = feature ? hasFeature : true;
  const active = pathname === href || pathname.startsWith(`${href}/`);

  if (!permissionOk || !featureOk) return null;

  return (
    <Link
      href={href}
      className={cn(
        "flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors",
        active
          ? "bg-brand-50 text-brand-700 dark:bg-brand-950/40 dark:text-brand-400"
          : "text-surface-600 hover:bg-surface-100 hover:text-surface-800"
      )}
    >
      <Icon size={18} />
      <span>{label}</span>
    </Link>
  );
}

export default function DashboardLayout({ children }: { children: React.ReactNode }) {
  const { user, isLoading, isAuthenticated, logout } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.replace("/login");
    }
  }, [isLoading, isAuthenticated, router]);

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <Spinner />
      </div>
    );
  }

  if (!isAuthenticated) return null;

  return (
    <div className="min-h-screen flex">
      {/* Sidebar */}
      <aside className="w-64 bg-surface-0 border-r border-surface-200 flex flex-col fixed inset-y-0 left-0 z-30">
        {/* Logo */}
        <div className="h-16 flex items-center gap-3 px-5 border-b border-surface-200">
          <div className="w-8 h-8 rounded-lg bg-brand-500 flex items-center justify-center flex-shrink-0">
            <WrenchIcon className="w-4 h-4 text-white" />
          </div>
          <div className="min-w-0">
            <p className="text-sm font-bold text-surface-900 font-display truncate">
              {user?.businessName}
            </p>
          </div>
        </div>

        {/* Nav */}
        <nav className="flex-1 px-3 py-4 space-y-1 overflow-y-auto">
          {NAV_ITEMS.map((item) => (
            <NavLink key={item.href} {...item} />
          ))}
        </nav>

        {/* User */}
        <div className="px-3 py-4 border-t border-surface-200">
          <div className="flex items-center gap-3 px-3 py-2">
            <div className="w-8 h-8 rounded-full bg-brand-100 dark:bg-brand-950/40 flex items-center justify-center text-brand-700 dark:text-brand-400 text-sm font-bold flex-shrink-0">
              {user?.name?.charAt(0)?.toUpperCase()}
            </div>
            <div className="min-w-0 flex-1">
              <p className="text-sm font-medium text-surface-900 truncate">{user?.name}</p>
              <p className="text-xs text-surface-500 truncate">{user?.email}</p>
            </div>
          </div>
          <ThemeToggle />
          <button
            onClick={logout}
            className="flex items-center gap-3 px-3 py-2 rounded-lg text-sm text-surface-500 hover:bg-surface-100 hover:text-surface-700 transition-colors w-full mt-1"
          >
            <LogOut size={16} />
            <span>Sign out</span>
          </button>
        </div>
      </aside>

      {/* Main content */}
      <main className="flex-1 ml-64">
        <div className="p-8 max-w-7xl mx-auto">{children}</div>
      </main>
    </div>
  );
}
