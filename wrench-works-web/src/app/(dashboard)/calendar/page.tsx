"use client";

import { useState, useMemo, useRef, useEffect, Fragment } from "react";
import { useApiQuery } from "@/hooks/use-api";
import { usePermission } from "@/hooks/use-permission";
import {
  Button, Badge, Modal, Input, Select, Textarea,
  PageHeader, Spinner, EmptyState,
} from "@/components/ui";
import { cn, formatTime, BOOKING_STATUS_COLORS , statusLabel} from "@/lib/utils";
import { Plus, Calendar as CalendarIcon, ChevronLeft, ChevronRight } from "lucide-react";
import {
  format, startOfWeek, endOfWeek, addWeeks, subWeeks, addDays,
  isSameDay, differenceInMinutes, startOfDay, setHours,
  startOfMonth, endOfMonth, subMonths, addMonths,
  isWithinInterval, isBefore, isAfter, max as dateMax, min as dateMin,
  getDay,
} from "date-fns";
import toast from "react-hot-toast";
import { ApiError } from "@/lib/fetcher";
import { mutate } from "swr";
import { ErrorState } from "@/components/data-state";

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
function describeBookingError(err: unknown, bookings: Booking[] | undefined): string {
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

interface Booking {
  id: string;
  zoneId: string;
  zoneName: string;
  zoneColor: string | null;
  customerId: string;
  customerName: string;
  vehicleId: string;
  vehicleDisplay: string;
  title: string;
  startUtc: string;
  endUtc: string;
  notes: string | null;
  status: string;
  jobId: string | null;
}

interface Zone {
  id: string;
  name: string;
  color: string | null;
  capacity: number;
  isActive: boolean;
}

type ViewMode = "week" | "month";

/* ══════════════════════════════════════════════════
   Helpers
   ══════════════════════════════════════════════════ */
const DAY_START = 0;
const DAY_END = 24;
const TOTAL_HRS = DAY_END - DAY_START;
const HR_PX = 60;
const GRID_H = TOTAL_HRS * HR_PX;
const HOURS = Array.from({ length: TOTAL_HRS }, (_, i) => DAY_START + i);
const SCROLL_TO_HOUR = 7;

function isMultiDay(b: Booking) {
  return !isSameDay(new Date(b.startUtc), new Date(b.endUtc));
}

/** Assign overlap lanes for bookings within one day-column. */
function assignLanes(bookings: Booking[]): { booking: Booking; lane: number; totalLanes: number }[] {
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
function getMultiDaySpan(
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
export default function CalendarPage() {
  const canEdit = usePermission("calendar.edit");
  const [view, setView] = useState<ViewMode>("week");
  const [weekStart, setWeekStart] = useState(() => startOfWeek(new Date(), { weekStartsOn: 1 }));
  const [monthDate, setMonthDate] = useState(() => new Date());
  const [showCreate, setShowCreate] = useState(false);
  const [selectedZone, setSelectedZone] = useState("all");
  const [selectedBooking, setSelectedBooking] = useState<Booking | null>(null);
  const [editingBooking, setEditingBooking] = useState<Booking | null>(null);

  // Compute query range
  const queryFrom = view === "week"
    ? weekStart
    : startOfWeek(startOfMonth(monthDate), { weekStartsOn: 1 });
  const queryTo = view === "week"
    ? endOfWeek(weekStart, { weekStartsOn: 1 })
    : endOfWeek(endOfMonth(monthDate), { weekStartsOn: 1 });

  const { data: bookings, isLoading, error: bookingsError, mutate: reloadBookings } = useApiQuery<Booking[]>(
    "/api/calendar/bookings",
    { from: queryFrom.toISOString(), to: queryTo.toISOString() }
  );
  const { data: zones, error: zonesError, mutate: reloadZones } = useApiQuery<Zone[]>("/api/zones");

  const activeZones = (zones ?? []).filter((z) => z.isActive);

  const filtered = useMemo(() => {
    const list = bookings ?? [];
    return selectedZone === "all" ? list : list.filter((b) => b.zoneId === selectedZone);
  }, [bookings, selectedZone]);

  // Nav handlers
  const goPrev = () => view === "week" ? setWeekStart(subWeeks(weekStart, 1)) : setMonthDate(subMonths(monthDate, 1));
  const goNext = () => view === "week" ? setWeekStart(addWeeks(weekStart, 1)) : setMonthDate(addMonths(monthDate, 1));
  const goToday = () => {
    const now = new Date();
    setWeekStart(startOfWeek(now, { weekStartsOn: 1 }));
    setMonthDate(now);
  };

  const headerDesc = view === "week"
    ? `${format(weekStart, "d MMM")} — ${format(endOfWeek(weekStart, { weekStartsOn: 1 }), "d MMM yyyy")}`
    : format(monthDate, "MMMM yyyy");

  return (
    <>
      <PageHeader
        title="Calendar"
        description={headerDesc}
        actions={
          <div className="flex items-center gap-2 flex-wrap">
            {/* Zone filter */}
            <select
              value={selectedZone}
              onChange={(e) => setSelectedZone(e.target.value)}
              className="rounded-lg border border-surface-300 bg-surface-0 px-3 py-2 text-sm text-surface-700 focus:outline-none focus:ring-2 focus:ring-brand-400"
            >
              <option value="all">All zones</option>
              {activeZones.map((z) => (
                <option key={z.id} value={z.id}>{z.name}</option>
              ))}
            </select>

            {/* View toggle */}
            <div className="flex border border-surface-300 rounded-lg overflow-hidden">
              {(["week", "month"] as const).map((v) => (
                <button
                  key={v}
                  onClick={() => setView(v)}
                  className={cn(
                    "px-3 py-2 text-sm font-medium capitalize transition-colors",
                    view === v
                      ? "bg-brand-500 text-white"
                      : "text-surface-600 hover:bg-surface-100"
                  )}
                >
                  {v}
                </button>
              ))}
            </div>

            {/* Nav */}
            <div className="flex items-center border border-surface-300 rounded-lg overflow-hidden">
              <button onClick={goPrev} className="px-2.5 py-2 hover:bg-surface-100 text-surface-600 transition-colors">
                <ChevronLeft size={16} />
              </button>
              <button onClick={goToday} className="px-3 py-2 text-sm font-medium hover:bg-surface-100 text-surface-700 border-x border-surface-300 transition-colors">
                Today
              </button>
              <button onClick={goNext} className="px-2.5 py-2 hover:bg-surface-100 text-surface-600 transition-colors">
                <ChevronRight size={16} />
              </button>
            </div>

            {canEdit && (
              <Button onClick={() => setShowCreate(true)}>
                <Plus size={16} /> New Booking
              </Button>
            )}
          </div>
        }
      />

      {isLoading ? (
        <div className="flex justify-center py-20"><Spinner /></div>
      ) : bookingsError || zonesError ? (
        <ErrorState
          error={bookingsError ?? zonesError}
          onRetry={() => { reloadBookings(); reloadZones(); }}
        />
      ) : activeZones.length === 0 ? (
        <EmptyState
          icon={<CalendarIcon size={48} />}
          title="No zones configured"
          description="Create bays/zones in Settings to start scheduling"
          action={<Button variant="secondary" onClick={() => { window.location.href = "/settings/zones"; }}>Go to Settings</Button>}
        />
      ) : view === "week" ? (
        <WeekView
          bookings={filtered}
          zones={activeZones}
          weekStart={weekStart}
          onSelectBooking={setSelectedBooking}
        />
      ) : (
        <MonthView
          bookings={filtered}
          zones={activeZones}
          monthDate={monthDate}
          onSelectBooking={setSelectedBooking}
        />
      )}

      {selectedBooking && (
        <BookingDetailModal
          booking={selectedBooking}
          zones={activeZones}
          onClose={() => setSelectedBooking(null)}
          onEdit={() => { setEditingBooking(selectedBooking); setSelectedBooking(null); }}
        />
      )}
      {editingBooking && (
        <EditBookingModal
          booking={editingBooking}
          zones={activeZones}
          bookings={bookings}
          onClose={() => setEditingBooking(null)}
          onSaved={() => {
            setEditingBooking(null);
            mutate((key: string) => typeof key === "string" && key.startsWith("/api/calendar"));
          }}
        />
      )}
      {showCreate && (
        <CreateBookingModal
          zones={activeZones}
          bookings={bookings}
          onClose={() => setShowCreate(false)}
          onCreated={() => {
            setShowCreate(false);
            mutate((key: string) => typeof key === "string" && key.startsWith("/api/calendar"));
          }}
        />
      )}
    </>
  );
}

/* ══════════════════════════════════════════════════
   Week View
   ══════════════════════════════════════════════════ */
function WeekView({
  bookings, zones, weekStart, onSelectBooking,
}: {
  bookings: Booking[];
  zones: Zone[];
  weekStart: Date;
  onSelectBooking: (b: Booking) => void;
}) {
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
              <div key={day.toISOString()} className={cn("relative border-r border-surface-200 last:border-r-0", isToday && "bg-brand-50/20 dark:bg-brand-950/10")}>
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

                  return (
                    <button
                      key={booking.id}
                      onClick={() => onSelectBooking(booking)}
                      className="absolute z-10 rounded-md px-1.5 py-1 text-left overflow-hidden transition-all hover:brightness-95 hover:shadow-md"
                      style={{
                        top,
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
function MonthView({
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
function BookingDetailModal({
  booking, zones, onClose, onEdit,
}: { booking: Booking; zones: Zone[]; onClose: () => void; onEdit: () => void }) {
  const zone = zones.find((z) => z.id === booking.zoneId);
  const canEdit = usePermission("calendar.edit");
  const [deleting, setDeleting] = useState(false);
  const [status, setStatusPending] = useState<string | null>(null);

  /** Completed / NoShow were unreachable before PATCH /bookings/{id}/status existed. */
  const setStatus = async (next: string) => {
    setStatusPending(next);
    try {
      const { fetcher } = await import("@/lib/fetcher");
      await fetcher.patch(`/api/calendar/bookings/${booking.id}/status`, { status: next });
      toast.success(next === "NoShow" ? "Marked as no-show" : "Marked completed");
      mutate((key: string) => typeof key === "string" && key.startsWith("/api/calendar"));
      onClose();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Failed to update status");
    } finally {
      setStatusPending(null);
    }
  };

  const handleDelete = async () => {
    setDeleting(true);
    try {
      const { fetcher } = await import("@/lib/fetcher");
      await fetcher.delete(`/api/calendar/bookings/${booking.id}`);
      toast.success("Booking cancelled");
      mutate((key: string) => typeof key === "string" && key.startsWith("/api/calendar"));
      onClose();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Failed to cancel");
    } finally {
      setDeleting(false);
    }
  };

  const multi = isMultiDay(booking);

  return (
    <Modal open onClose={onClose} title={booking.title}>
      <div className="space-y-4">
        <div className="grid grid-cols-2 gap-4 text-sm">
          <div>
            <p className="text-surface-500">Customer</p>
            <p className="font-medium text-surface-900">{booking.customerName}</p>
          </div>
          <div>
            <p className="text-surface-500">Vehicle</p>
            <p className="font-medium text-surface-900">{booking.vehicleDisplay || "—"}</p>
          </div>
          <div>
            <p className="text-surface-500">Zone</p>
            <div className="flex items-center gap-2">
              <div className="w-2.5 h-2.5 rounded-full" style={{ backgroundColor: zone?.color ?? "#6b7280" }} />
              <p className="font-medium text-surface-900">{booking.zoneName}</p>
            </div>
          </div>
          <div>
            <p className="text-surface-500">Status</p>
            <Badge className={BOOKING_STATUS_COLORS[booking.status]}>{booking.status}</Badge>
          </div>
          <div>
            <p className="text-surface-500">Start</p>
            <p className="font-medium text-surface-900">
              {format(new Date(booking.startUtc), multi ? "EEE d MMM yyyy, HH:mm" : "EEE d MMM, HH:mm")}
            </p>
          </div>
          <div>
            <p className="text-surface-500">End</p>
            <p className="font-medium text-surface-900">
              {format(new Date(booking.endUtc), multi ? "EEE d MMM yyyy, HH:mm" : "EEE d MMM, HH:mm")}
            </p>
          </div>
        </div>

        {booking.notes && (
          <div className="text-sm">
            <p className="text-surface-500">Notes</p>
            <p className="text-surface-700 mt-1">{booking.notes}</p>
          </div>
        )}

        {booking.jobId && (
          <a href={`/jobs/${booking.jobId}`} className="inline-flex items-center gap-1.5 text-sm text-brand-600 hover:text-brand-700 font-medium dark:text-brand-400">
            View linked job →
          </a>
        )}

        {canEdit && booking.status === "Confirmed" && (
          <div className="flex items-center justify-between gap-2 pt-2 border-t border-surface-200">
            <div className="flex gap-2">
              <Button variant="secondary" size="sm" onClick={onEdit}>
                Edit
              </Button>
              <Button variant="ghost" size="sm" loading={status === "Completed"} onClick={() => setStatus("Completed")}>
                Mark completed
              </Button>
              <Button variant="ghost" size="sm" loading={status === "NoShow"} onClick={() => setStatus("NoShow")}>
                No-show
              </Button>
            </div>
            <Button variant="danger" size="sm" loading={deleting} onClick={handleDelete}>
              Cancel Booking
            </Button>
          </div>
        )}
      </div>
    </Modal>
  );
}

/* ══════════════════════════════════════════════════
   Create Booking Modal
   ══════════════════════════════════════════════════ */
function CreateBookingModal({ zones, bookings, onClose, onCreated }: { zones: Zone[]; bookings: Booking[] | undefined; onClose: () => void; onCreated: () => void }) {
  const [form, setForm] = useState({
    zoneId: zones[0]?.id ?? "",
    customerId: "",
    vehicleId: "",
    title: "",
    startUtc: "",
    endUtc: "",
    notes: "",
    createJob: true,
  });
  const [customerSearch, setCustomerSearch] = useState("");
  const [loading, setLoading] = useState(false);

  const { data: searchResults } = useApiQuery<{ id: string; name: string; phone?: string }[]>(
    customerSearch.length >= 2 ? "/api/customers/search" : null,
    { q: customerSearch }
  );

  const { data: customerDetail } = useApiQuery<{ vehicles: { id: string; displayName: string; registration?: string }[] }>(
    form.customerId ? `/api/customers/${form.customerId}` : null
  );
  const vehicles = customerDetail?.vehicles ?? [];

  const update = (field: string) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) =>
    setForm((f) => ({ ...f, [field]: e.target.value }));

  const canSubmit = form.customerId && form.vehicleId && form.title && form.startUtc && form.endUtc;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.customerId || !form.vehicleId) {
      toast.error("Please select a customer and vehicle");
      return;
    }
    setLoading(true);
    try {
      const { fetcher } = await import("@/lib/fetcher");
      await fetcher.post("/api/calendar/bookings", {
        ...form,
        startUtc: new Date(form.startUtc).toISOString(),
        endUtc: new Date(form.endUtc).toISOString(),
      });
      toast.success("Booking created");
      onCreated();
    } catch (err: unknown) {
      toast.error(describeBookingError(err, bookings));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal open onClose={onClose} title="New Booking" wide>
      <form onSubmit={handleSubmit} className="space-y-4">
        <Select
          id="zoneId"
          label="Zone / Bay"
          value={form.zoneId}
          onChange={update("zoneId")}
          options={zones.filter((z) => z.isActive).map((z) => ({ value: z.id, label: z.name }))}
        />

        <div>
          <Input
            id="customerSearch"
            label="Customer"
            placeholder="Search by name or phone..."
            value={customerSearch}
            onChange={(e) => setCustomerSearch(e.target.value)}
          />
          {searchResults && searchResults.length > 0 && !form.customerId && (
            <div className="mt-1 border border-surface-200 rounded-lg max-h-40 overflow-y-auto bg-surface-0">
              {searchResults.map((c) => (
                <button
                  key={c.id}
                  type="button"
                  className="w-full text-left px-3 py-2 text-sm hover:bg-surface-50 transition-colors"
                  onClick={() => {
                    setForm((f) => ({ ...f, customerId: c.id, vehicleId: "" }));
                    setCustomerSearch(c.name);
                  }}
                >
                  {c.name} {c.phone && <span className="text-surface-400">· {c.phone}</span>}
                </button>
              ))}
            </div>
          )}
        </div>

        {form.customerId && vehicles.length > 0 && (
          <Select
            id="vehicleId"
            label="Vehicle"
            value={form.vehicleId}
            onChange={update("vehicleId")}
            placeholder="Select vehicle"
            options={vehicles.map((v) => ({
              value: v.id,
              label: [v.displayName, v.registration].filter(Boolean).join(" · "),
            }))}
          />
        )}

        {form.customerId && customerDetail && vehicles.length === 0 && (
          <p className="text-sm text-surface-500">This customer has no vehicles. Add one from the customer page first.</p>
        )}

        <Input id="title" label="Title" required value={form.title} onChange={update("title")} placeholder="e.g. Full service" />

        <div className="grid grid-cols-2 gap-4">
          <Input id="startUtc" label="Start" type="datetime-local" required value={form.startUtc} onChange={update("startUtc")} />
          <Input id="endUtc" label="End" type="datetime-local" required value={form.endUtc} onChange={update("endUtc")} />
        </div>

        <Textarea id="notes" label="Notes" value={form.notes} onChange={update("notes")} />

        <label className="flex items-center gap-2 text-sm text-surface-700">
          <input
            type="checkbox"
            checked={form.createJob}
            onChange={(e) => setForm((f) => ({ ...f, createJob: e.target.checked }))}
            className="rounded border-surface-300"
          />
          Also create a linked job
        </label>

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={loading} disabled={!canSubmit}>Create Booking</Button>
        </div>
      </form>
    </Modal>
  );
}

/* ══════════════════════════════════════════════════
   Edit Booking
   ══════════════════════════════════════════════════ */
/**
 * Edit an existing booking.
 *
 * Before this existed a booking was immutable: changing a time meant cancel-and-
 * recreate, and cancelling closes the linked job. Customer and vehicle are shown but
 * not editable here — moving a booking to a different car is closer to a new booking
 * than an edit, and doing it silently would strand the linked job's history.
 */
function EditBookingModal({
  booking, zones, bookings, onClose, onSaved,
}: {
  booking: Booking;
  zones: Zone[];
  bookings: Booking[] | undefined;
  onClose: () => void;
  onSaved: () => void;
}) {
  /** datetime-local wants "yyyy-MM-ddTHH:mm" in LOCAL time, not an ISO UTC string. */
  const toLocalInput = (iso: string) => format(new Date(iso), "yyyy-MM-dd'T'HH:mm");

  const [form, setForm] = useState({
    zoneId: booking.zoneId,
    title: booking.title,
    startUtc: toLocalInput(booking.startUtc),
    endUtc: toLocalInput(booking.endUtc),
    notes: booking.notes ?? "",
  });
  const [loading, setLoading] = useState(false);

  const update = (field: string) => (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>
  ) => setForm((f) => ({ ...f, [field]: e.target.value }));

  const canSubmit = Boolean(form.zoneId && form.title.trim() && form.startUtc && form.endUtc);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!canSubmit) return;
    setLoading(true);
    try {
      const { fetcher } = await import("@/lib/fetcher");
      await fetcher.put(`/api/calendar/bookings/${booking.id}`, {
        zoneId: form.zoneId,
        customerId: booking.customerId,
        vehicleId: booking.vehicleId,
        title: form.title.trim(),
        startUtc: new Date(form.startUtc).toISOString(),
        endUtc: new Date(form.endUtc).toISOString(),
        notes: form.notes || null,
      });
      toast.success("Booking updated");
      onSaved();
    } catch (err: unknown) {
      toast.error(describeBookingError(err, bookings));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal open onClose={onClose} title="Edit Booking" wide>
      <form onSubmit={handleSubmit} className="space-y-4">
        <div className="text-sm text-surface-500">
          {booking.customerName} · {booking.vehicleDisplay}
        </div>

        <Select
          id="zoneId"
          label="Zone / Bay"
          value={form.zoneId}
          onChange={update("zoneId")}
          options={zones.map((z) => ({ value: z.id, label: z.name }))}
        />

        <Input id="title" label="Title" value={form.title} onChange={update("title")} required />

        <div className="grid grid-cols-2 gap-4">
          <Input id="startUtc" label="Start" type="datetime-local" value={form.startUtc} onChange={update("startUtc")} required />
          <Input id="endUtc" label="End" type="datetime-local" value={form.endUtc} onChange={update("endUtc")} required />
        </div>

        <Textarea id="notes" label="Notes" value={form.notes} onChange={update("notes")} />

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={loading} disabled={!canSubmit}>Save Changes</Button>
        </div>
      </form>
    </Modal>
  );
}
