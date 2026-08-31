"use client";

import { useState } from "react";
import { useApi } from "@/hooks/use-api";
import { Button, Modal, Input, Textarea, Select } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import toast from "react-hot-toast";
import type { JobDetail } from "../_lib/job";

export function EditJobModal({ job, onClose, onSaved }: { job: JobDetail; onClose: () => void; onSaved: () => void }) {
  // Convert UTC ISO to datetime-local value (YYYY-MM-DDTHH:mm)
  const toLocal = (iso: string | null) => {
    if (!iso) return "";
    const d = new Date(iso);
    const offset = d.getTimezoneOffset();
    const local = new Date(d.getTime() - offset * 60000);
    return local.toISOString().slice(0, 16);
  };

  const { data: zones } = useApi<{ id: string; name: string; isActive: boolean }[]>("/api/zones");
  const activeZones = (zones ?? []).filter((z) => z.isActive);

  const [form, setForm] = useState({
    title: job.title,
    internalNotes: job.internalNotes ?? "",
    customerNotes: job.customerNotes ?? "",
    priority: job.priority,
    scheduledStart: toLocal(job.scheduledStartUtc),
    scheduledEnd: toLocal(job.scheduledEndUtc),
  });
  const [loading, setLoading] = useState(false);
  const u = (f: string) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) =>
    setForm((p) => ({ ...p, [f]: e.target.value }));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try {
      await fetcher.put(`/api/jobs/${job.id}`, {
        title: form.title,
        internalNotes: form.internalNotes || null,
        customerNotes: form.customerNotes || null,
        priority: form.priority,
        zoneId: null,
        scheduledStartUtc: form.scheduledStart ? new Date(form.scheduledStart).toISOString() : null,
        scheduledEndUtc: form.scheduledEnd ? new Date(form.scheduledEnd).toISOString() : null,
      });
      toast.success("Job updated");
      onSaved();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Failed");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal open onClose={onClose} title="Edit Job" wide>
      <form onSubmit={handleSubmit} className="space-y-4">
        <Input id="title" label="Title" required value={form.title} onChange={u("title")} />

        <Select
          id="priority"
          label="Priority"
          value={form.priority}
          onChange={u("priority")}
          options={[
            { value: "Low", label: "Low" },
            { value: "Normal", label: "Normal" },
            { value: "High", label: "High" },
            { value: "Urgent", label: "Urgent" },
          ]}
        />

        <div className="grid grid-cols-2 gap-4">
          <Input
            id="scheduledStart"
            label="Scheduled Start"
            type="datetime-local"
            value={form.scheduledStart}
            onChange={u("scheduledStart")}
          />
          <Input
            id="scheduledEnd"
            label="Scheduled End"
            type="datetime-local"
            value={form.scheduledEnd}
            onChange={u("scheduledEnd")}
          />
        </div>

        <Textarea id="internalNotes" label="Internal Notes" value={form.internalNotes} onChange={u("internalNotes")} />
        <Textarea id="customerNotes" label="Customer Notes" value={form.customerNotes} onChange={u("customerNotes")} />

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={loading}>Save Changes</Button>
        </div>
      </form>
    </Modal>
  );
}
