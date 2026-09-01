import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

// Moved to lib/currency.ts, which knows the supported set and picks a matching locale.
// Re-exported so existing imports keep working — but prefer useCurrency() in client
// components: calling this bare silently formats as GBP regardless of the business.
export { formatCurrency } from "@/lib/currency";

export function formatDate(date: string | Date, options?: Intl.DateTimeFormatOptions) {
  return new Date(date).toLocaleDateString("en-GB", {
    day: "numeric",
    month: "short",
    year: "numeric",
    ...options,
  });
}

export function formatTime(date: string | Date) {
  return new Date(date).toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit" });
}

export function formatDateTime(date: string | Date) {
  return `${formatDate(date)} ${formatTime(date)}`;
}

/**
 * Display labels for the raw status enums the API returns.
 *
 * Badges used to render the enum verbatim ("InProgress", "WaitingParts") while the
 * filter dropdown beside them showed "In Progress" — the same status spelled two ways
 * on one screen. Use `statusLabel()` anywhere a status is shown to a user.
 */
export const JOB_STATUS_LABELS: Record<string, string> = {
  Draft: "Draft",
  Scheduled: "Scheduled",
  InProgress: "In Progress",
  WaitingParts: "Waiting Parts",
  Completed: "Completed",
  Invoiced: "Invoiced",
  Closed: "Closed",
};

export const BOOKING_STATUS_LABELS: Record<string, string> = {
  Confirmed: "Confirmed",
  Cancelled: "Cancelled",
  Completed: "Completed",
  NoShow: "No Show",
};

/** Falls back to splitting CamelCase, so a new enum value degrades readably. */
export function statusLabel(status: string): string {
  return (
    JOB_STATUS_LABELS[status] ??
    BOOKING_STATUS_LABELS[status] ??
    status.replace(/([a-z])([A-Z])/g, "$1 $2")
  );
}

export const JOB_STATUS_COLORS: Record<string, string> = {
  Draft: "bg-surface-200 text-surface-700",
  Scheduled: "bg-blue-100 text-blue-800 dark:bg-blue-950/40 dark:text-blue-300",
  InProgress: "bg-amber-100 text-amber-800 dark:bg-amber-950/40 dark:text-amber-300",
  WaitingParts: "bg-orange-100 text-orange-800 dark:bg-orange-950/40 dark:text-orange-300",
  Completed: "bg-green-100 text-green-800 dark:bg-green-950/40 dark:text-green-300",
  Invoiced: "bg-purple-100 text-purple-800 dark:bg-purple-950/40 dark:text-purple-300",
  Closed: "bg-surface-300 text-surface-600",
};

export const BOOKING_STATUS_COLORS: Record<string, string> = {
  Confirmed: "bg-blue-100 text-blue-800 dark:bg-blue-950/40 dark:text-blue-300",
  Cancelled: "bg-red-100 text-red-800 dark:bg-red-950/40 dark:text-red-300",
  Completed: "bg-green-100 text-green-800 dark:bg-green-950/40 dark:text-green-300",
  NoShow: "bg-surface-300 text-surface-600",
};
