"use client";

import { format, startOfWeek, endOfWeek, addDays, isSameDay, startOfDay, startOfMonth, endOfMonth, isBefore, isAfter, getDay, max as dateMax, min as dateMin } from "date-fns";
import { Modal } from "@/components/ui";
import { cn, formatTime } from "@/lib/utils";
import { isMultiDay } from "../_lib/booking";
import type { Booking, Zone } from "../_lib/booking";

export function MonthView({
  bookings, zones, monthDate, onSelectBooking,
}: {
  bookings: Booking[];
  zones: Zone[];
  monthDate: Date;
  onSelectBooking: (b: Booking) => void;
}) {
  const mStart = startOfMonth(monthDate);
  const mEnd = endOfMonth(monthDate);
  const gridStart = startOfWeek(mStart, { weekStartsOn: 1 });
  const gridEnd = endOfWeek(mEnd, { weekStartsOn: 1 });
  const today = new Date();

  // Build weeks
  const weeks: Date[][] = [];
  let cur = gridStart;
  while (isBefore(cur, gridEnd) || isSameDay(cur, gridEnd)) {
    const week: Date[] = [];
    for (let d = 0; d < 7; d++) {
      week.push(addDays(cur, d));
    }
    weeks.push(week);
    cur = addDays(cur, 7);
  }

  return (
    <div className="border border-surface-200 rounded-xl bg-surface-0 overflow-hidden shadow-sm">
      {/* Day-of-week header */}
      <div className="grid grid-cols-7 border-b border-surface-200">
        {["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => (
          <div key={d} className="py-2 text-center text-[11px] font-medium text-surface-500 uppercase tracking-wider border-r border-surface-200 last:border-r-0">
            {d}
          </div>
        ))}
      </div>

      {/* Week rows */}
      {weeks.map((week, wIdx) => {
        const rowStart = week[0];
        const rowEnd = addDays(week[6], 1); // exclusive end

        // Bookings that touch this week
        const weekBookings = bookings.filter((b) => {
          const bs = new Date(b.startUtc);
          const be = new Date(b.endUtc);
          return isBefore(bs, rowEnd) && isAfter(be, rowStart);
        }).sort((a, b) => {
          // Multi-day first, then by start
          const aMulti = isMultiDay(a) ? 0 : 1;
          const bMulti = isMultiDay(b) ? 0 : 1;
          if (aMulti !== bMulti) return aMulti - bMulti;
          return new Date(a.startUtc).getTime() - new Date(b.startUtc).getTime();
        });

        // Assign visual rows for this week
        const slotRows: { booking: Booking; startCol: number; span: number }[][] = [];
        for (const bk of weekBookings) {
          const bs = new Date(bk.startUtc);
          const be = new Date(bk.endUtc);
          const clampedStart = dateMax([bs, rowStart]);
          const clampedEnd = dateMin([be, rowEnd]);
          const startCol = (getDay(clampedStart) + 6) % 7; // Mon=0
          const endDay = addDays(clampedEnd, isSameDay(clampedEnd, startOfDay(clampedEnd)) && !isSameDay(clampedEnd, clampedStart) ? -1 : 0);
          const endCol = (getDay(endDay) + 6) % 7;
          const span = Math.max(1, endCol - startCol + 1);

          let placed = false;
          for (const row of slotRows) {
            const conflicts = row.some((r) => startCol < r.startCol + r.span && startCol + span > r.startCol);
            if (!conflicts) { row.push({ booking: bk, startCol, span }); placed = true; break; }
          }
          if (!placed) slotRows.push([{ booking: bk, startCol, span }]);
        }

        const rowHeight = Math.max(100, 28 + slotRows.length * 24 + 4);

        return (
          <div key={wIdx} className="relative border-b border-surface-200 last:border-b-0" style={{ minHeight: rowHeight }}>
            {/* Day cells */}
            <div className="grid grid-cols-7 h-full">
              {week.map((day) => {
                const isCurrentMonth = day.getMonth() === monthDate.getMonth();
                const isToday = isSameDay(day, today);
                return (
                  <div
                    key={day.toISOString()}
                    className={cn(
                      "border-r border-surface-200 last:border-r-0 p-1",
                      !isCurrentMonth && "bg-surface-50/50 dark:bg-surface-50/5",
                      isToday && "bg-brand-50/30 dark:bg-brand-950/15"
                    )}
                  >
                    <div className={cn(
                      "text-sm font-medium px-1",
                      isToday ? "text-brand-600 dark:text-brand-400 font-bold" : isCurrentMonth ? "text-surface-700" : "text-surface-400"
                    )}>
                      {format(day, "d")}
                    </div>
                  </div>
                );
              })}
            </div>

            {/* Booking chips — positioned over the entire row */}
            {slotRows.map((row, rIdx) =>
              row.map((slot) => {
                const zone = zones.find((z) => z.id === slot.booking.zoneId);
                const color = zone?.color ?? "#6b7280";
                return (
                  <button
                    key={slot.booking.id}
                    onClick={() => onSelectBooking(slot.booking)}
                    className="absolute z-10 rounded px-1.5 text-[11px] font-medium truncate leading-[22px] h-[22px] transition-opacity hover:opacity-80"
                    style={{
                      top: 26 + rIdx * 24,
                      left: `calc(${(slot.startCol / 7) * 100}% + 2px)`,
                      width: `calc(${(slot.span / 7) * 100}% - 4px)`,
                      backgroundColor: `${color}22`,
                      borderLeft: `2px solid ${color}`,
                      color,
                    }}
                  >
                    {!isMultiDay(slot.booking) && (
                      <span className="text-surface-400 mr-1">{formatTime(slot.booking.startUtc)}</span>
                    )}
                    {slot.booking.title}
                  </button>
                );
              })
            )}
          </div>
        );
      })}
    </div>
  );
}

/* ══════════════════════════════════════════════════
   Booking Detail Modal
   ══════════════════════════════════════════════════ */
