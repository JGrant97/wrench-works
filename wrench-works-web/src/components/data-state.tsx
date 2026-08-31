"use client";

import { AlertCircle, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui";

/**
 * The missing third state: a fetch that failed.
 *
 * Pages used to render only loading and empty. SWR leaves `data` undefined on failure
 * and flips `isLoading` false, so a 500 was indistinguishable from a real empty result —
 * a failed /api/zones told an admin "No zones configured", a failed vehicle load said
 * "Vehicle not found", and a failed vehicle search rendered nothing at all. The user was
 * confidently told the wrong thing and given nothing to act on.
 * See docs/review-findings.md finding 3.
 *
 *   const { data, isLoading, error, mutate } = useApi<Zone[]>("/api/zones");
 *   {isLoading ? <Spinner />
 *    : error ? <ErrorState error={error} onRetry={() => mutate()} compact />
 *    : zones.length === 0 ? <p>No zones configured yet</p>
 *    : ...}
 *
 * Put the error branch BEFORE the empty branch. That ordering is the whole fix.
 */

/**
 * ApiError carries a real message from the server (lib/fetcher.ts joins validation
 * field errors into it). Anything else gets a generic line rather than leaking an
 * internal string like "Failed to fetch" into the UI.
 */
export function errorMessage(error: unknown): string {
  if (error instanceof Error && error.message) return error.message;
  return "Something went wrong loading this.";
}

export function ErrorState({
  error,
  onRetry,
  compact,
}: {
  error: unknown;
  onRetry?: () => void;
  compact?: boolean;
}) {
  return (
    <div
      role="alert"
      className={
        compact
          ? "flex items-center gap-2 py-3 text-sm text-red-600"
          : "flex flex-col items-center justify-center gap-3 py-12 text-center"
      }
    >
      <AlertCircle size={compact ? 14 : 24} className="text-red-500 shrink-0" />
      <p className="text-sm text-red-600">{errorMessage(error)}</p>
      {onRetry && (
        <Button variant="secondary" size="sm" onClick={onRetry}>
          <RefreshCw size={14} /> Try again
        </Button>
      )}
    </div>
  );
}
