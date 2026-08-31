"use client";

import { useState } from "react";
import { format } from "date-fns";
import { Button, Modal, Input, Select, Textarea } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import toast from "react-hot-toast";
import { describeBookingError } from "../_lib/booking";
import type { Booking, Zone } from "../_lib/booking";

export function EditBookingModal({
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
