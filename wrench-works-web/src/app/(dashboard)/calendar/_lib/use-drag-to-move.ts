"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { HR_PX } from "./booking";
import type { Booking } from "./booking";

/**
 * Drag-to-move for the week grid.
 *
 * `PUT /bookings/{id}/move` has existed, conflict-checked and cascading to the linked job,
 * since the calendar was built — with nothing calling it. Rescheduling is the most routine
 * thing a service advisor does, so this is the missing half rather than a new feature.
 *
 * Pointer events rather than HTML5 drag-and-drop: the grid is absolutely positioned by
 * time, so the drop position has to be read as pixels and converted back to minutes.
 * HTML5 drag gives neither reliable coordinates nor a live preview.
 */

/** Minutes to snap to. Matching the grid's half-hour lines would be too coarse to be useful. */
const SNAP_MINUTES = 15;

/**
 * Pointer travel before a press counts as a drag. Without it every click registers as a
 * one-pixel move and silently reschedules the booking the user meant to open.
 */
const DRAG_THRESHOLD_PX = 4;

export interface DragState {
  bookingId: string;
  /** Live vertical offset in pixels, for rendering the block under the cursor. */
  deltaY: number;
  /** Whether the pointer has travelled far enough to count as a drag rather than a click. */
  active: boolean;
}

export function snapMinutes(minutes: number): number {
  return Math.round(minutes / SNAP_MINUTES) * SNAP_MINUTES;
}

export function useDragToMove({
  enabled,
  onMove,
}: {
  enabled: boolean;
  /** Receives the booking and its new start/end as UTC ISO strings. */
  onMove: (booking: Booking, startUtc: string, endUtc: string) => void;
}) {
  const [drag, setDrag] = useState<DragState | null>(null);

  // Held in a ref, not state: the pointermove handler needs the current values without
  // re-subscribing the listener on every pixel of movement.
  const origin = useRef<{ booking: Booking; startX: number; startY: number } | null>(null);

  const beginDrag = useCallback(
    (booking: Booking, e: React.PointerEvent) => {
      if (!enabled) return;
      // Left button only — a right-click drag should open the context menu, not reschedule.
      if (e.button !== 0) return;

      origin.current = { booking, startX: e.clientX, startY: e.clientY };
      setDrag({ bookingId: booking.id, deltaY: 0, active: false });
    },
    [enabled]
  );

  useEffect(() => {
    if (!drag) return;

    const handleMove = (e: PointerEvent) => {
      const start = origin.current;
      if (!start) return;

      const deltaY = e.clientY - start.startY;
      const travelled = Math.abs(deltaY) + Math.abs(e.clientX - start.startX);

      setDrag((d) => (d ? { ...d, deltaY, active: d.active || travelled > DRAG_THRESHOLD_PX } : d));
    };

    const handleUp = (e: PointerEvent) => {
      const start = origin.current;
      origin.current = null;

      // A press that never travelled is a click; the block's onClick opens the booking.
      const wasDragging = drag?.active ?? false;
      setDrag(null);

      // The PUT is fired HERE, not inside a setDrag updater. React treats updaters as pure
      // and double-invokes them under StrictMode, so a side effect placed in one sends the
      // request twice — and the loser of that race surfaced as a spurious "Someone else
      // changed this while you were working on it" toast on a move that had succeeded.
      if (!start || !wasDragging) return;

      {
        const deltaMinutes = snapMinutes((e.clientY - start.startY) / HR_PX * 60);

        // Which day column the pointer was released over. Reading the DOM is what lets a
        // booking move across days as well as up and down.
        const target = document.elementFromPoint(e.clientX, e.clientY);
        const dayIso = target?.closest<HTMLElement>("[data-day]")?.dataset.day;

        const originalStart = new Date(start.booking.startUtc);
        const originalEnd = new Date(start.booking.endUtc);
        const durationMs = originalEnd.getTime() - originalStart.getTime();

        const newStart = new Date(originalStart);
        newStart.setMinutes(newStart.getMinutes() + deltaMinutes);

        if (dayIso) {
          const targetDay = new Date(dayIso);
          newStart.setFullYear(targetDay.getFullYear(), targetDay.getMonth(), targetDay.getDate());
        }

        // Dropped exactly where it started: nothing to save, and a no-op PUT would still
        // spend a round trip and re-render the grid.
        if (newStart.getTime() !== originalStart.getTime()) {
          onMove(
            start.booking,
            newStart.toISOString(),
            new Date(newStart.getTime() + durationMs).toISOString()
          );
        }
      }
    };

    window.addEventListener("pointermove", handleMove);
    window.addEventListener("pointerup", handleUp);
    // Releasing outside the window would otherwise leave the grid stuck mid-drag.
    window.addEventListener("pointercancel", handleUp);

    return () => {
      window.removeEventListener("pointermove", handleMove);
      window.removeEventListener("pointerup", handleUp);
      window.removeEventListener("pointercancel", handleUp);
    };
  }, [drag, onMove]);

  return { drag, beginDrag };
}
