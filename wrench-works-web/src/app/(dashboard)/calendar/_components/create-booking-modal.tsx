"use client";

import { useState } from "react";
import { useCustomerVehicle } from "@/hooks/use-customer-vehicle";
import { Button, Modal, Input, Select, Textarea } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import toast from "react-hot-toast";
import { describeBookingError } from "../_lib/booking";
import type { Booking, Zone } from "../_lib/booking";

export function CreateBookingModal({ zones, bookings, onClose, onCreated }: { zones: Zone[]; bookings: Booking[] | undefined; onClose: () => void; onCreated: () => void }) {
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

  const { customers: searchResults, vehicles, customerLoaded } =
    useCustomerVehicle(customerSearch, form.customerId);

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

        {form.customerId && customerLoaded && vehicles.length === 0 && (
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
