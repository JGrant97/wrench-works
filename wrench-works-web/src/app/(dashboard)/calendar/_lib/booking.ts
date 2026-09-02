/**
 * Shared types, layout constants and pure helpers for the calendar.
 *
 * Split out of page.tsx (which was 1002 lines holding six components) so the week and
 * month grids can share the lane-packing and multi-day maths without either importing
 * the other. Everything here is pure — no fetching, no React.
 */
import { isSameDay, isBefore, isAfter, getDay, max as dateMax, min as dateMin } from "date-fns";
import { formatTime } from "@/lib/utils";
import { ApiError } from "@/lib/fetcher";

// Re-exported from the generated client so these names stay stable for the
// components that import them, while the shapes come from the API contract.
import type { BookingDto as Booking, ZoneDto as Zone } from "@/api/generated/models";
export type { Booking, Zone };

/* ══════════════════════════════════════════════════
   Types
   ══════════════════════════════════════════════════ */
/**
 * Turns a booking API error into something a service advisor can act on.
 *
 * A 409 carries `details.conflictingBookingIds`, which the UI used to discard — the
 * user saw "Booking conflicts detected" with no idea what they had clashed with, which
 * is the one thing they need in order to resolve it. If the clashing booking is on
 * screen we can name it and give its times.
 */
export function describeBookingError(err: unknown, bookings: Booking[] | undefined): string {
  if (!(err instanceof ApiError)) {
    return err instanceof Error ? err.message : "Failed to save booking";
  }

  const ids = (err.details as { conflictingBookingIds?: string[] } | undefined)?.conflictingBookingIds;
  if (!ids?.length) return err.message;

  const clashes = (bookings ?? []).filter((b) => ids.includes(b.id));
  if (clashes.length === 0) {
    return `That slot is already taken by ${ids.length} other booking${ids.length === 1 ? "" : "s"} in this bay.`;
  }

  const described = clashes
    .map((b) => `${b.title} (${formatTime(b.startUtc)}–${formatTime(b.endUtc)})`)
    .join(", ");

  return `Clashes with ${described} in ${clashes[0].zoneName}.`;
}



export type ViewMode = "week" | "month";

/* ══════════════════════════════════════════════════
   Helpers
   ══════════════════════════════════════════════════ */
export const DAY_START = 0;
export const DAY_END = 24;
export const TOTAL_HRS = DAY_END - DAY_START;
export const HR_PX = 60;
export const GRID_H = TOTAL_HRS * HR_PX;
export const HOURS = Array.from({ length: TOTAL_HRS }, (_, i) => DAY_START + i);
export const SCROLL_TO_HOUR = 7;

export function isMultiDay(b: Booking) {
  return !isSameDay(new Date(b.startUtc), new Date(b.endUtc));
}

/** Assign overlap lanes for bookings within one day-column. */
export function assignLanes(bookings: Booking[]): { booking: Booking; lane: number; totalLanes: number }[] {
  if (bookings.length === 0) return [];
  const sorted = [...bookings].sort(
    (a, b) => new Date(a.startUtc).getTime() - new Date(b.startUtc).getTime()
  );
  const lanes: { end: Date; booking: Booking }[][] = [];

  for (const bk of sorted) {
    const s = new Date(bk.startUtc);
    let placed = false;
    for (let i = 0; i < lanes.length; i++) {
      const last = lanes[i][lanes[i].length - 1];
      if (!isAfter(s, last.end) && !isSameDay(s, last.end)) continue;
      if (s >= last.end) {
        lanes[i].push({ end: new Date(bk.endUtc), booking: bk });
        placed = true;
        break;
      }
    }
    if (!placed) lanes.push([{ end: new Date(bk.endUtc), booking: bk }]);
  }

  // Build overlap groups to get totalLanes per cluster
  const result: { booking: Booking; lane: number; totalLanes: number }[] = [];
  const items = lanes.flatMap((lane, laneIdx) =>
    lane.map((l) => ({ booking: l.booking, lane: laneIdx }))
  );

  // For each booking, figure out how many lanes overlap with it
  for (const item of items) {
    const s = new Date(item.booking.startUtc).getTime();
    const e = new Date(item.booking.endUtc).getTime();
    const overlapping = items.filter((other) => {
      const os = new Date(other.booking.startUtc).getTime();
      const oe = new Date(other.booking.endUtc).getTime();
      return os < e && oe > s;
    });
    const maxLane = Math.max(...overlapping.map((o) => o.lane));
    result.push({ ...item, totalLanes: maxLane + 1 });
  }

  return result;
}

/** Get the span of a multi-day booking within a given week row. */
export function getMultiDaySpan(
  b: Booking,
  rowStart: Date,
  rowEnd: Date
): { startCol: number; span: number } | null {
  const bs = new Date(b.startUtc);
  const be = new Date(b.endUtc);
  if (isAfter(bs, rowEnd) || isBefore(be, rowStart)) return null;
  const clampedStart = dateMax([bs, rowStart]);
  const clampedEnd = dateMin([be, rowEnd]);
  const startCol = getDay(clampedStart) === 0 ? 6 : getDay(clampedStart) - 1; // Mon=0
  const endCol = getDay(clampedEnd) === 0 ? 6 : getDay(clampedEnd) - 1;
  return { startCol, span: endCol - startCol + 1 };
}

/* ══════════════════════════════════════════════════
   Main Page
   ══════════════════════════════════════════════════ */
