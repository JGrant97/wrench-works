"use client";

import { useMemo, useRef, useEffect, Fragment } from "react";
import { format, endOfWeek, addDays, isSameDay, differenceInMinutes, startOfDay, setHours } from "date-fns";
import { cn, formatTime } from "@/lib/utils";
import { isMultiDay, assignLanes, getMultiDaySpan, DAY_START, TOTAL_HRS, HR_PX, GRID_H, HOURS, SCROLL_TO_HOUR } from "../_lib/booking";
import type { Booking, Zone } from "../_lib/booking";
import { useDragToMove } from "../_lib/use-drag-to-move";

export function WeekView({
  bookings, zones, weekStart, onSelectBooking, onMoveBooking, canEdit = false,
}: {
  bookings: Booking[];
  zones: Zone[];
  weekStart: Date;
  onSelectBooking: (b: Booking) => void;
  /** Omitted, or canEdit false, leaves the grid read-only. */
  onMoveBooking?: (b: Booking, startUtc: string, endUtc: string) => void;
  canEdit?: boolean;
}) {
  const { drag, beginDrag } = useDragToMove({
    enabled: canEdit && Boolean(onMoveBooking),
    onMove: (b, startUtc, endUtc) => onMoveBooking?.(b, startUtc, endUtc),
  });
  const weekEnd = endOfWeek(weekStart, { weekStartsOn: 1 });
  const days = Array.from({ length: 7 }, (_, i) => addDays(weekStart, i));
  const today = new Date();

  // Split into multi-day and single-day
  const multiDay = bookings.filter(isMultiDay);
  const singleDay = bookings.filter((b) => !isMultiDay(b));

  // Compute multi-day row slots (stack them vertically)
  const multiDayRows = useMemo(() => {
    const rows: Booking[][] = [];
    const sorted = [...multiDay].sort(
      (a, b) => new Date(a.startUtc).getTime() - new Date(b.startUtc).getTime()
    );
    for (const bk of sorted) {
      const span = getMultiDaySpan(bk, weekStart, weekEnd);
      if (!span) continue;
      let placed = false;
      for (const row of rows) {
        const conflicts = row.some((existing) => {
          const es = getMultiDaySpan(existing, weekStart, weekEnd);
          if (!es) return false;
          return span.startCol < es.startCol + es.span && span.startCol + span.span > es.startCol;
        });
        if (!conflicts) { row.push(bk); placed = true; break; }
      }
      if (!placed) rows.push([bk]);
    }
    return rows;
  }, [multiDay, weekStart, weekEnd]);

  const scrollRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = SCROLL_TO_HOUR * HR_PX;
    }
  }, []);

  return (
    <div className="border border-surface-200 rounded-xl bg-surface-0 overflow-hidden shadow-sm">
      {/* ── Day headers ── */}
      <div className="grid border-b border-surface-200" style={{ gridTemplateColumns: "56px repeat(7, 1fr)" }}>
        <div className="border-r border-surface-200" />
        {days.map((day) => {
          const isToday = isSameDay(day, today);
          return (
            <div key={day.toISOString()} className={cn("py-2.5 text-center border-r border-surface-200 last:border-r-0", isToday && "bg-brand-50/50 dark:bg-brand-950/20")}>
              <p className="text-[11px] font-medium text-surface-500 uppercase tracking-wider">{format(day, "EEE")}</p>
              <p className={cn("text-lg font-semibold", isToday ? "text-brand-600 dark:text-brand-400" : "text-surface-900")}>{format(day, "d")}</p>
            </div>
          );
        })}
      </div>

      {/* ── Multi-day banner ── */}
      {multiDayRows.length > 0 && (
        <div className="border-b border-surface-200">
          {multiDayRows.map((row, rowIdx) => (
            <div key={rowIdx} className="relative" style={{ height: 28, marginLeft: 56 }}>
              {row.map((bk) => {
                const span = getMultiDaySpan(bk, weekStart, weekEnd);
                if (!span) return null;
                const zone = zones.find((z) => z.id === bk.zoneId);
                const color = zone?.color ?? "#6b7280";
                return (
                  <button
                    key={bk.id}
                    onClick={() => onSelectBooking(bk)}
                    className="absolute top-0.5 bottom-0.5 rounded-md px-2 flex items-center gap-1.5 text-[11px] font-semibold truncate transition-opacity hover:opacity-80 z-10"
                    style={{
                      left: `calc(${(span.startCol / 7) * 100}% + 2px)`,
                      width: `calc(${(span.span / 7) * 100}% - 4px)`,
                      backgroundColor: `${color}22`,
                      borderLeft: `3px solid ${color}`,
                      color,
                    }}
                  >
                    <div className="w-1.5 h-1.5 rounded-full shrink-0" style={{ backgroundColor: color }} />
                    {bk.title} — {bk.customerName}
                  </button>
                );
              })}
            </div>
          ))}
        </div>
      )}

      {/* ── Time grid ── */}
      <div ref={scrollRef} className="overflow-y-auto" style={{ maxHeight: multiDayRows.length > 0 ? `calc(100vh - ${280 + multiDayRows.length * 28}px)` : "calc(100vh - 260px)" }}>
        <div className="grid relative" style={{ gridTemplateColumns: "56px repeat(7, 1fr)", height: GRID_H }}>
          {/* Hour gutter */}
          <div className="border-r border-surface-200 relative">
            {HOURS.map((h) => (
              <div key={h} className="absolute right-2 -translate-y-1/2 text-[11px] text-surface-400 font-mono tabular-nums" style={{ top: (h - DAY_START) * HR_PX }}>
                {`${String(h).padStart(2, "0")}:00`}
              </div>
            ))}
          </div>

          {/* Day columns */}
          {days.map((day) => {
            const isToday = isSameDay(day, today);
            const dayStart = setHours(startOfDay(day), DAY_START);
            const dayBookings = singleDay.filter((b) => isSameDay(new Date(b.startUtc), day));
            const laned = assignLanes(dayBookings);

            return (
              <div
                key={day.toISOString()}
                data-day={day.toISOString()}
                className={cn("relative border-r border-surface-200 last:border-r-0", isToday && "bg-brand-50/20 dark:bg-brand-950/10")}
              >
                {/* Grid lines */}
                {HOURS.map((h) => (
                  <Fragment key={h}>
                    <div className="absolute inset-x-0 border-t border-surface-100" style={{ top: (h - DAY_START) * HR_PX }} />
                    <div className="absolute inset-x-0 border-t border-dashed border-surface-100/50" style={{ top: (h - DAY_START) * HR_PX + HR_PX / 2 }} />
                  </Fragment>
                ))}

                {/* Now line */}
                {isToday && (() => {
                  const now = new Date();
                  const mins = now.getHours() * 60 + now.getMinutes() - DAY_START * 60;
                  if (mins < 0 || mins > TOTAL_HRS * 60) return null;
                  return (
                    <div className="absolute inset-x-0 z-20 pointer-events-none" style={{ top: (mins / 60) * HR_PX }}>
                      <div className="flex items-center"><div className="w-2 h-2 rounded-full bg-red-500 -ml-1 shrink-0" /><div className="flex-1 h-[2px] bg-red-500" /></div>
                    </div>
                  );
                })()}

                {/* Booking blocks */}
                {laned.map(({ booking, lane, totalLanes }) => {
                  const s = new Date(booking.startUtc);
                  const e = new Date(booking.endUtc);
                  const topMin = Math.max(0, differenceInMinutes(s, dayStart));
                  const durMin = Math.max(15, differenceInMinutes(e, s));
                  const top = (topMin / 60) * HR_PX;
                  const height = Math.max(20, (durMin / 60) * HR_PX);
                  const zone = zones.find((z) => z.id === booking.zoneId);
                  const color = zone?.color ?? "#6b7280";
                  const widthPct = 100 / totalLanes;
                  const leftPct = lane * widthPct;

                  const dragging = drag?.bookingId === booking.id && drag.active;
                  const dragOffset = dragging ? drag.deltaY : 0;

                  return (
                    <button
                      key={booking.id}
                      onPointerDown={(e) => beginDrag(booking, e)}
                      onClick={() => {
                        // A completed drag ends with drag still set for this render; opening
                        // the modal here would fight the reschedule the user just made.
                        if (dragging) return;
                        onSelectBooking(booking);
                      }}
                      className={cn(
                        "absolute z-10 rounded-md px-1.5 py-1 text-left overflow-hidden hover:brightness-95 hover:shadow-md",
                        canEdit && "cursor-grab",
                        dragging ? "z-30 cursor-grabbing shadow-lg opacity-90" : "transition-all"
                      )}
                      style={{
                        top: top + dragOffset,
                        height,
                        left: `calc(${leftPct}% + 2px)`,
                        width: `calc(${widthPct}% - 4px)`,
                        backgroundColor: `${color}1a`,
                        borderLeft: `3px solid ${color}`,
                      }}
                    >
                      <p className="text-[11px] font-semibold truncate leading-tight" style={{ color }}>{booking.title}</p>
                      {height > 30 && <p className="text-[10px] text-surface-500 truncate">{booking.customerName}</p>}
                      {height > 46 && <p className="text-[10px] text-surface-400 truncate">{formatTime(booking.startUtc)} – {formatTime(booking.endUtc)}</p>}
                      {height > 62 && booking.vehicleDisplay && <p className="text-[10px] text-surface-400 truncate">{booking.vehicleDisplay}</p>}
                      {height > 78 && booking.zoneName && <p className="text-[10px] text-surface-400 truncate">{booking.zoneName}</p>}
                    </button>
                  );
                })}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}

/* ══════════════════════════════════════════════════
   Month View
   ══════════════════════════════════════════════════ */
