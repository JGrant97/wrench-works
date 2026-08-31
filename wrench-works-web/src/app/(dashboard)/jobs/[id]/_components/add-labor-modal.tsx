"use client";

import { useState } from "react";
import { Button, Modal, Input } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import toast from "react-hot-toast";

export function AddLaborModal({ jobId, onClose, onAdded }: { jobId: string; onClose: () => void; onAdded: () => void }) {
  const [form, setForm] = useState({ description: "", hours: "", rate: "" });
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try {
      await fetcher.post(`/api/jobs/${jobId}/labor`, {
        description: form.description,
        hours: parseFloat(form.hours),
        rate: parseFloat(form.rate),
      });
      toast.success("Labor added");
      onAdded();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Failed");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal open onClose={onClose} title="Add Labor">
      <form onSubmit={handleSubmit} className="space-y-4">
        <Input id="desc" label="Description" required value={form.description} onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))} placeholder="e.g. Brake pad replacement" />
        <div className="grid grid-cols-2 gap-4">
          <Input id="hours" label="Hours" type="number" step="0.25" min="0" required value={form.hours} onChange={(e) => setForm((f) => ({ ...f, hours: e.target.value }))} />
          <Input id="rate" label="Rate (£/hr)" type="number" step="0.01" min="0" required value={form.rate} onChange={(e) => setForm((f) => ({ ...f, rate: e.target.value }))} />
        </div>
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={loading}>Add</Button>
        </div>
      </form>
    </Modal>
  );
}
