"use client";

import { useState, useMemo, useCallback } from "react";
import { format, startOfWeek, endOfWeek, addWeeks, subWeeks, startOfMonth, endOfMonth, subMonths, addMonths } from "date-fns";
import { Plus, ChevronLeft, ChevronRight, Calendar as CalendarIcon } from "lucide-react";
import { mutate } from "swr";
import { useApiQuery } from "@/hooks/use-api";
import { usePermission } from "@/hooks/use-permission";
import { Button, PageHeader, Spinner, EmptyState } from "@/components/ui";
import { ErrorState } from "@/components/data-state";
import { cn } from "@/lib/utils";
import { fetcher } from "@/lib/fetcher";
import toast from "react-hot-toast";
import { describeBookingError } from "./_lib/booking";
import type { Booking, Zone, ViewMode } from "./_lib/booking";
import { WeekView } from "./_components/week-view";
import { MonthView } from "./_components/month-view";
import { BookingDetailModal } from "./_components/booking-detail-modal";
import { CreateBookingModal } from "./_components/create-booking-modal";
import { EditBookingModal } from "./_components/edit-booking-modal";

export default function CalendarPage() {
  const canEdit = usePermission("calendar.edit");

  const [selectedBooking, setSelectedBooking] = useState<Booking | null>(null);
  const [editingBooking, setEditingBooking] = useState<Booking | null>(null);
  const [view, setView] = useState<ViewMode>("week");
  const [weekStart, setWeekStart] = useState(() => startOfWeek(new Date(), { weekStartsOn: 1 }));
  const [monthDate, setMonthDate] = useState(() => new Date());
  const [showCreate, setShowCreate] = useState(false);
  const [selectedZone, setSelectedZone] = useState("all");

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

  /**
   * Drag-to-move. Calls PUT /bookings/{id}/move, which was written with conflict checking
   * and a job-schedule cascade and then never called by anything until now.
   *
   * Not optimistic: the server may reject the drop as a double-booking, and a block that
   * snaps into place and then jumps back is worse than one that waits. The request is
   * fast and the toast explains any rejection.
   */
  const handleMoveBooking = useCallback(
    async (booking: Booking, startUtc: string, endUtc: string) => {
      try {
        await fetcher.put(`/api/calendar/bookings/${booking.id}/move`, {
          zoneId: booking.zoneId,
          startUtc,
          endUtc,
        });
        toast.success("Booking moved");
      } catch (err) {
        // describeBookingError turns a 409 into the clashing booking's name and times.
        toast.error(describeBookingError(err, bookings));
      } finally {
        // Refetch either way: on success to pick up the server's version, on failure to
        // put the block back where it actually is.
        mutate((key: string) => typeof key === "string" && key.startsWith("/api/calendar"));
      }
    },
    [bookings]
  );

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
          canEdit={canEdit}
          onMoveBooking={handleMoveBooking}
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
