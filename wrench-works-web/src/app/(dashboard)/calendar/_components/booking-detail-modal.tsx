"use client";

import { useState } from "react";
import { format } from "date-fns";
import { mutate } from "swr";
import { usePermission } from "@/hooks/use-permission";
import { Button, Badge, Modal } from "@/components/ui";
import { BOOKING_STATUS_COLORS } from "@/lib/utils";
import { fetcher } from "@/lib/fetcher";
import toast from "react-hot-toast";
import { isMultiDay } from "../_lib/booking";
import type { Booking, Zone } from "../_lib/booking";

export function BookingDetailModal({
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
